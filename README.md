# 🎬 MovieHub API

API REST para gerenciamento de filmes, reviews e favoritos, com autenticação JWT e controle de acesso por roles.

---

## 🚀 Tecnologias

- **.NET 8** — framework principal
- **ASP.NET Core** — web API
- **Entity Framework Core** — ORM
- **PostgreSQL** — banco de dados
- **Redis** — blacklist de tokens JWT
- **ASP.NET Identity** — gerenciamento de usuários e roles
- **JWT Bearer** — autenticação e autorização
- **AutoMapper** — mapeamento de objetos
- **Swagger / OpenAPI** — documentação e testes da API
- **Docker + Docker Compose** — containerização
- **xUnit + FluentAssertions** — testes unitários

---

## 📦 Como rodar

### Pré-requisitos
- [Docker](https://www.docker.com/)
- [Docker Compose](https://docs.docker.com/compose/)

### Passos

```bash
# Clone o repositório
git clone https://github.com/gui-andreis/MovieHub.git
cd MovieHub

# Suba os containers
docker compose -f MovieHub/docker-compose.yml up --build
```

A API estará disponível em:
```
http://localhost:5000
```

O Swagger estará disponível em:
```
http://localhost:5000/swagger
```

> Na primeira execução, o banco de dados é criado automaticamente via migrations e um usuário **Admin** padrão é gerado.

### Credenciais do Admin padrão
```
Email: admin@moviehub.com
Password: Admin@123
```

---

## 🔐 Autenticação

A API utiliza **JWT Bearer Token**. Para acessar endpoints protegidos:

1. Registre um usuário em `POST /api/auth/register`
2. Faça login em `POST /api/auth/login`
3. Copie o token retornado
4. No Swagger, clique em **Authorize 🔒** e insira: `Bearer {seu_token}`

### Logout
O logout invalida o token via blacklist no Redis — o token não pode ser reutilizado mesmo antes de expirar.

### Roles
| Role | Permissões |
|------|-----------|
| **Admin** | CRUD completo de filmes e gêneros, deletar qualquer review |
| **User** | Criar/editar/deletar próprias reviews, gerenciar favoritos |

---

## 📋 Endpoints

### 🔑 Auth
| Método | Rota | Descrição | Auth |
|--------|------|-----------|------|
| POST | `/api/auth/register` | Registrar novo usuário | ❌ |
| POST | `/api/auth/login` | Login e geração de token | ❌ |
| POST | `/api/auth/logout` | Logout e invalidação do token | 🔒 User |

### 🎬 Movies
| Método | Rota | Descrição | Auth |
|--------|------|-----------|------|
| GET | `/api/movies` | Listar filmes (paginado + filtros) | ❌ |
| GET | `/api/movies/{id}` | Buscar filme por ID | ❌ |
| POST | `/api/movies` | Criar filme (com imagem opcional) | 🔒 Admin |
| PUT | `/api/movies/{id}` | Atualizar filme | 🔒 Admin |
| DELETE | `/api/movies/{id}` | Deletar filme | 🔒 Admin |

#### Filtros disponíveis em `GET /api/movies`
| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `title` | string | Busca parcial por título |
| `releaseYear` | int | Ano de lançamento exato |
| `genreIds` | int[] | Um ou mais IDs de gênero |
| `minRating` | double | Nota mínima |
| `maxRating` | double | Nota máxima |
| `orderBy` | string | `title`, `title_desc`, `year`, `year_desc` |
| `pageNumber` | int | Página (padrão: 1) |
| `pageSize` | int | Itens por página (padrão: 10, máx: 50) |

### 🎭 Genres
| Método | Rota | Descrição | Auth |
|--------|------|-----------|------|
| GET | `/api/genre` | Listar gêneros disponíveis | ❌ |
| POST | `/api/genre` | Criar gênero | 🔒 Admin |
| DELETE | `/api/genre/{id}` | Deletar gênero | 🔒 Admin |

### ⭐ Reviews
| Método | Rota | Descrição | Auth |
|--------|------|-----------|------|
| GET | `/api/review/movie/{movieId}` | Reviews de um filme | ❌ |
| GET | `/api/review/my` | Minhas reviews | 🔒 User |
| POST | `/api/review` | Criar review | 🔒 User |
| PUT | `/api/review/{id}` | Editar review | 🔒 Dono |
| DELETE | `/api/review/{id}` | Deletar review | 🔒 Dono / Admin |

### ❤️ Favorites
| Método | Rota | Descrição | Auth |
|--------|------|-----------|------|
| GET | `/api/favorite/my` | Meus favoritos | 🔒 User |
| POST | `/api/favorite` | Adicionar favorito | 🔒 User |
| DELETE | `/api/favorite/{movieId}` | Remover favorito | 🔒 User |

---

## 📬 Formato das respostas

Todos os endpoints retornam um envelope padronizado:

```json
{
  "status": 200,
  "message": "Filmes recuperados com sucesso.",
  "data": { }
}
```

Erros seguem o mesmo formato:
```json
{
  "status": 404,
  "message": "Filme com id 1 não encontrado.",
  "data": null
}
```

---

## 🗂️ Estrutura do Projeto

```
MovieHub/
├── Common/               # Classes compartilhadas (ApiResponse, AppControllerBase)
├── Controllers/          # Endpoints da API
├── Data/
│   └── Dtos/             # Objetos de transferência de dados
│       ├── Auth/
│       ├── Favorite/
│       ├── Genre/
│       ├── Movie/
│       ├── Review/
│       └── User/
├── Exceptions/           # Exceptions customizadas (Not Found, Conflict, Forbidden...)
├── Extensions/           # Extension methods (JWT, Identity, Services, Middlewares)
├── Middleware/           # Middleware global de exceções
├── Migrations/           # Migrations do banco de dados
├── Models/               # Entidades do domínio
├── Pagination/           # Classes de paginação
├── Profiles/             # Perfis do AutoMapper
├── Queries/              # Parâmetros de query (filtros/paginação)
│   └── Movies/
└── Services/
    ├── Implementations/  # Implementações dos services
    └── Interfaces/       # Contratos dos services

MovieHub.Tests/
├── Helpers/              # TestDbContextFactory (banco em memória)
└── Services/             # Testes unitários por service
```

---

## 🔧 Variáveis de Ambiente

Configure no `docker-compose.yml`:

```yaml
ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=movieHubDB;Username=postgres;Password=admin
ConnectionStrings__Redis=redis:6379
```

Ou no `appsettings.json`:

```json
"JwtSettings": {
  "SecretKey": "sua_chave_secreta_aqui_minimo_32_chars",
  "Issuer": "MovieHubAPI",
  "Audience": "MovieHubAPI",
  "ExpirationInMinutes": 60
},
"AdminSettings": {
  "Email": "admin@moviehub.com",
  "Password": "Admin@123"
}
```

---

## 🧪 Testes

```bash
cd MovieHub.Tests
dotnet test
```

Cobertura atual: **30 testes unitários** cobrindo `MovieService`, `ReviewService`, `FavoriteService` e `GenreService`.

---

## 🛣️ Roadmap

### ✅ Concluído
- Autenticação JWT com logout via blacklist (Redis)
- CRUD completo de filmes com upload de imagem opcional
- Gêneros com relacionamento N:N
- Filtros, busca e paginação em filmes
- Reviews com restrição de duplicata por usuário
- Favoritos com validação de conflito
- Padronização de responses com envelope `{ data, message, status }`
- Exceptions semânticas por tipo (404, 400, 401, 403, 409)
- Testes unitários com xUnit + FluentAssertions (30 testes)
- CancellationToken em todas as operações async
- DeleteBehavior.Cascade configurado explicitamente

### 🔧 Próximas melhorias
- [ ] Rate limiting
- [ ] Cache em endpoints de leitura (`GET /movies`)
- [ ] Refresh token + rotação de tokens
- [ ] Migração de imagens para S3/Cloudflare R2
- [ ] Front-end React
- [ ] Melhoria contínua de código e arquitetura

---

## 👨‍💻 Autor

Feito por **Guilherme Andreis**  
[LinkedIn](https://www.linkedin.com/in/guilherme-boeira-andreis) · [GitHub](https://github.com/gui-andreis)