# 🎬 MovieHub API v2

API REST de uma plataforma estilo streaming (inspirada em Netflix e Prime Video). Ela entrega um catálogo de **filmes e séries** importado do TMDB, com **perfis por conta**, **Minha lista**, **continuar assistindo** (com o próximo episódio automático), **reações**, **reviews**, **classificação indicativa** e uma home personalizada.

Faz parte do ecossistema MovieHub:

| Repositório | Papel | Deploy |
|---|---|---|
| **moviehub-api** (este) | Catálogo, contas, perfis e dados pessoais | Render (Docker) + Neon + Upstash |
| [moviehub-web](https://github.com/gui-andreis/moviehub-web) | Front-end em Next.js (BFF) | Vercel |
| [moviehub-ai](https://github.com/gui-andreis/MovieHubAI) | Assistente com IA (LangGraph + OpenAI) | Render |

---

## 🧱 Stack

- **.NET 8 / ASP.NET Core**: Web API com controllers
- **EF Core 8 + PostgreSQL** (Npgsql), com migrations aplicadas automaticamente na subida
- **ASP.NET Identity + JWT** com **refresh token rotativo** e detecção de reuso
- **Redis** (opcional) para a blacklist de access tokens no logout. Sem Redis, cai para memória
- **TMDB**: importador em background com throttling (token bucket) e retry/circuit breaker (`Microsoft.Extensions.Http.Resilience`)
- **Serilog** com logs estruturados no console
- **Output cache**, **compressão Brotli/Gzip** e **rate limiting** por usuário/IP
- **Swagger**: `/swagger`, com suporte a `Bearer` + `X-Profile-Id`
- **xUnit + FluentAssertions**: 52 testes

---

## 🏗️ Arquitetura

```mermaid
flowchart LR
    U[Navegador] --> W[Next.js / Vercel<br/>BFF com cookies httpOnly]
    W -- "Bearer + X-Profile-Id" --> A[MovieHub API<br/>Render]
    W -- SSE --> IA[MovieHub AI<br/>Render]
    IA -- "Bearer + X-Profile-Id" --> A
    A --> PG[(PostgreSQL<br/>Neon)]
    A --> R[(Redis<br/>Upstash)]
    A -- import --> T[TMDB API]
```

```
MovieHub/
├── Common/            # ApiResponse (envelope), PagedResult, normalização de texto, classificação indicativa
├── Controllers/       # Auth, Profiles, Catalog, Me, Reviews, Admin
├── Data/              # DbContext + factory de design-time
├── Dtos/              # Contratos de entrada/saída
├── Exceptions/        # Exceções semânticas (400/401/403/404/409)
├── Extensions/        # DI: banco, auth, Redis, HTTP, Swagger, startup (migrations + seed)
├── Middleware/        # Tratamento global de erros
├── Migrations/
├── Models/            # Entidades do domínio
└── Services/
    ├── Auth/          # AuthService, RefreshTokenService, ProfileService, blacklist
    ├── Catalog/       # Catálogo, home, "me", reviews, admin, projeções/filtros
    ├── Tmdb/          # Client, modelos, importador, fila + BackgroundService
    └── CurrentProfile.cs   # Resolve e valida o perfil do header X-Profile-Id
```

### Modelo de domínio

```mermaid
erDiagram
    ApplicationUser ||--o{ Profile : "até 5"
    ApplicationUser ||--o{ RefreshToken : ""
    Profile ||--o{ MyListItem : ""
    Profile ||--o{ WatchProgress : ""
    Profile ||--o{ Reaction : ""
    Profile ||--o{ Review : ""
    Title ||--o{ TitleGenre : ""
    Genre ||--o{ TitleGenre : ""
    Title ||--o{ TitleKeyword : ""
    Keyword ||--o{ TitleKeyword : ""
    Title ||--o{ TitleProvider : "stream/rent/buy"
    WatchProvider ||--o{ TitleProvider : ""
    Title }o--o| Collection : "saga"
    Title ||--o{ Credit : "cast/crew"
    Person ||--o{ Credit : ""
    Title ||--o{ Season : ""
    Season ||--o{ Episode : ""
    WatchProgress }o--o| Episode : ""
    Title {
        string type "movie | series"
        string name
        string overview
        string ageRating "L,10,12,14,16,18"
        string trailerKey "YouTube"
        string logoUrl
        double voteAverage
        double popularity
    }
```

- Um **Title** pode ser filme ou série; séries têm temporadas e episódios.
- Gênero ↔ título é **N:N**. Os gêneros de TV do TMDB (ex.: "Sci-Fi & Fantasy") são mapeados para os de filme, o que unifica o catálogo.
- Tudo que é pessoal pertence a um **Profile**. Perfis infantis ou com limite de classificação **filtram automaticamente** todas as listagens. Títulos sem classificação ficam ocultos para esses perfis.

---

## 🚀 Rodando localmente

Pré-requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download) e [Docker](https://www.docker.com/).

```bash
# 1. Infraestrutura (Postgres em 5438 e Redis em 6379)
docker compose up -d postgres redis

# 2. (opcional, mas recomendado) token do TMDB para popular o catálogo
#    themoviedb.org → Settings → API → "API Read Access Token" (eyJ...)
dotnet user-secrets set "Tmdb:ReadAccessToken" "eyJ..." --project MovieHub

# 3. API
cd MovieHub
dotnet run
# → http://localhost:5085/swagger
```

Ou tudo em containers: `cp .env.example .env` (preencha `TMDB_READ_ACCESS_TOKEN`) e `docker compose up --build`, com a API em `http://localhost:5000`.

Na primeira subida, a API:
1. cria o banco `moviehub_v2` e aplica as migrations;
2. cria as roles e o admin (`admin@moviehub.com` / `Admin@123`, configuráveis);
3. se o catálogo estiver vazio e houver token do TMDB, importa em background **~300 filmes e ~120 séries**, com sinopse em pt-BR, pôster, backdrop, logo, trailer, elenco, onde assistir no Brasil, classificação, sagas e episódios. O progresso aparece em `GET /api/admin/import/status`.

> Sem o token, a API sobe normalmente com o catálogo vazio e avisa no log. Depois de configurar o token, reinicie ou chame `POST /api/admin/import`.

### Testes

```bash
dotnet test MovieHub.slnx
```

São 52 testes cobrindo filtros e busca sem acento, maturidade/perfil infantil, similares, Minha lista, progresso e próximo episódio, reações, estatísticas, reviews (unicidade, permissões e distribuição), rotação e reuso de refresh token, perfis, o importador do TMDB ponta a ponta (com respostas simuladas) e o parsing de `DATABASE_URL`/Upstash.

---

## 🔐 Autenticação e perfis

1. `POST /api/auth/register` ou `/login` → `{ accessToken, refreshToken, expiresAt, user }`. Toda conta nasce com 1 perfil.
2. Envie `Authorization: Bearer <accessToken>`.
3. Escolha um perfil (`GET /api/profiles`) e envie `X-Profile-Id: <guid>` nos endpoints pessoais. Se o perfil for de outro usuário, a resposta é **403**.
4. O access token expira em 30 min. `POST /api/auth/refresh` troca o refresh token por um novo par, e o token antigo é invalidado. **Reusar** um refresh token já usado revoga todos os tokens do usuário.
5. `POST /api/auth/logout` coloca o access token na blacklist e revoga os refresh tokens.

---

## 📋 Endpoints (`/api`)

Todas as respostas usam o envelope `{ "status": 200, "message": "…", "data": … }`. As paginadas trazem `data = { items, page, pageSize, totalCount, totalPages }`. Erros de validação adicionam `errors: { campo: [mensagens] }`.

### Auth
| Método | Rota | Descrição |
|---|---|---|
| POST | `/auth/register` | `{ name, email, password }` |
| POST | `/auth/login` | `{ email, password }` |
| POST | `/auth/refresh` | `{ refreshToken }` (rotação) |
| POST | `/auth/logout` | 🔒 |
| GET | `/auth/me` | 🔒 |

### Perfis 🔒
| Método | Rota | Descrição |
|---|---|---|
| GET | `/profiles` | Perfis da conta |
| POST | `/profiles` | `{ name, avatarKey?, isKids, maturityLimit?, language? }` (máx. 5) |
| PUT | `/profiles/{id}` | Editar |
| DELETE | `/profiles/{id}` | Remover (não remove o último) |
| GET | `/profiles/avatars` | `avatar-01` … `avatar-12` |

### Catálogo (público; personaliza com Bearer + X-Profile-Id)
| Método | Rota | Descrição |
|---|---|---|
| GET | `/browse/home?type=all\|movie\|series` | Hero e linhas: continuar assistindo, minha lista, Top 10, em alta, "Porque você curtiu X", lançamentos, aclamados, gêneros, sagas, escondidos que valem a pena |
| GET | `/titles` | Filtros: `q`, `type`, `genreIds`, `yearFrom`, `yearTo`, `minRating`, `minRuntime`, `maxRuntime`, `providerIds`, `maxAgeRating`, `language`, `sort` (`popularity`\|`rating`\|`release_desc`\|`release_asc`\|`name`\|`added`), `page`, `pageSize` (≤ 50) |
| GET | `/titles/{id}` | Detalhe completo: elenco, equipe, onde assistir, temporadas, saga, nota interna e bloco `me` |
| GET | `/titles/{id}/similar?limit=12` | Parecidos (gêneros, palavras-chave e saga) |
| GET | `/titles/{id}/seasons/{n}` | Episódios (+ progresso por episódio) |
| GET | `/titles/trending` · `/titles/top10` · `/titles/upcoming` | Listas prontas |
| GET | `/titles/random?type=&genreId=` | "Surpreenda-me" |
| GET | `/search?q=` | Títulos e pessoas, sem acento |
| GET | `/genres` · `/genres/{slug}/titles` | Gêneros com contagem e títulos por gênero |
| GET | `/people/{id}` | Pessoa, "conhecido por" e filmografia (biografia buscada sob demanda no TMDB) |
| GET | `/collections/{id}` | Saga em ordem cronológica |
| GET | `/providers` | Serviços de streaming no BR |

### Pessoais 🔒 + `X-Profile-Id`
| Método | Rota | Descrição |
|---|---|---|
| GET / PUT / DELETE | `/me/list` · `/me/list/{titleId}` | Minha lista |
| GET | `/me/continue-watching` | Itens em andamento |
| PUT | `/me/progress` | `{ titleId, episodeId?, positionSeconds, durationSeconds }`: a partir de 90%, o item é concluído; em séries, o próximo episódio entra na fila |
| DELETE | `/me/progress/{titleId}` | Tirar de "continuar assistindo" |
| PUT / DELETE | `/me/reactions/{titleId}` | `{ value: "dislike" \| "like" \| "love" }` |
| GET | `/me/reviews` · `/me/stats` | Minhas reviews e estatísticas (assistidos, minutos, gêneros favoritos) |

### Reviews
| Método | Rota | Descrição |
|---|---|---|
| GET | `/titles/{id}/reviews?sort=newest\|oldest\|rating_desc\|rating_asc` | Paginado |
| GET | `/titles/{id}/reviews/summary` | Média, total e distribuição de 1 a 10 |
| POST | `/titles/{id}/reviews` | 🔒 `{ rating 1–10, comment?, containsSpoilers }` (uma por perfil) |
| PUT / DELETE | `/reviews/{id}` | 🔒 Dono (DELETE também por Admin) |

### Admin 🔒 role `Admin`
| Método | Rota | Descrição |
|---|---|---|
| GET | `/admin/stats` | Contagens e títulos mais adicionados às listas |
| POST | `/admin/import` | `{ type: "movie"\|"series"\|"both", pages: 1–25 }`, executado em background |
| GET | `/admin/import/status` | Andamento da importação |
| POST | `/admin/titles/{tmdbId}/import?type=movie\|series` | Importa/atualiza um título |
| POST / PUT / DELETE | `/admin/titles[/{id}]` | CRUD manual |
| PATCH | `/admin/titles/{id}/featured` | `{ isFeatured }` (destaques do hero) |
| POST / PUT / DELETE | `/admin/genres[/{id}]` | Gêneros |

`GET /health` → `{"status":"ok"}`, usado pelo health check do Render.

---

## 🔧 Variáveis de ambiente

| Variável | Obrigatória | Descrição |
|---|---|---|
| `DATABASE_URL` **ou** `ConnectionStrings__DefaultConnection` | ✅ | `postgresql://…?sslmode=require` (Neon) ou string Npgsql |
| `Jwt__SecretKey` | ✅ | ≥ 32 caracteres |
| `Jwt__AccessTokenMinutes` / `Jwt__RefreshTokenDays` | | Padrão: 30 min / 30 dias |
| `ConnectionStrings__Redis` | | `rediss://default:senha@host.upstash.io:6379` ou `host:port`. Sem ele, usa memória |
| `Cors__AllowedOrigins` | | Separadas por vírgula, ex.: `https://moviehub.vercel.app,https://*.vercel.app` |
| `Admin__Email` / `Admin__Password` | | Admin criado na primeira subida |
| `Tmdb__ReadAccessToken` | | Token v4 (`eyJ…`); também aceita a API key v3 |
| `Tmdb__SeedMovies` / `Tmdb__SeedSeries` | | Padrão: 300 / 120 |
| `Tmdb__AutoSeed` | | `true` (padrão): importa quando o catálogo está vazio |
| `Tmdb__MaxSeasonsWithEpisodes` | | Padrão: 10 (temporadas mais recentes com episódios) |
| `PORT` | | Injetada pelo Render |

---

## ☁️ Deploy (Render + Neon + Upstash, tudo no plano gratuito)

1. **Neon** ([neon.tech](https://neon.tech)): crie um projeto e copie a *connection string* (`postgresql://…?sslmode=require`).
2. **Upstash** ([upstash.com](https://upstash.com)), opcional: crie um Redis e copie a URL `rediss://…`.
3. **Render** ([render.com](https://render.com)): **New → Blueprint** apontando para este repositório. O `render.yaml` já define o serviço Docker, o health check e as variáveis. Preencha:
   - `DATABASE_URL`, `ConnectionStrings__Redis`, `Tmdb__ReadAccessToken`
   - `Admin__Email`, `Admin__Password`
   - `Cors__AllowedOrigins` com a URL do front na Vercel
   - `Jwt__SecretKey`, que o Render gera sozinho
4. Faça o deploy e acompanhe os logs: migrations → admin → importação do TMDB.

> No plano free, o Render "dorme" depois de 15 min sem acesso e a primeira requisição leva cerca de 30 s. A importação inicial roda só quando o banco está vazio.

---

## 👨‍💻 Autor

Feito por **Guilherme Andreis**
[LinkedIn](https://www.linkedin.com/in/guilherme-boeira-andreis) · [GitHub](https://github.com/gui-andreis)
