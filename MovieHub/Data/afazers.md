# 🎬 MovieHub — Roadmap de Melhorias

---

## ✅ Vale muito a pena — Faça logo

### 🔧 Qualidade de código (baixo esforço, alto impacto)
- **`readonly` nas injeções de dependência** dos services e controllers
  - Ex: `private readonly IMovieService _movieService;`
  - Evita reatribuição acidental, é boa prática padrão no .NET
- **Mover `if (coisa == null)` para os services** ✅
  - Controllers não devem ter lógica de negócio; o service lança a exception, o middleware captura ✅
- **Trocar middleware para `IExceptionHandler`** (interface oficial do .NET 8)
  - Mais idiomático que middleware manual, melhor integração com o pipeline
- **Adicionar `ConflictException`** (HTTP 409)
  - Usar em: usuário já existe, filme duplicado, review duplicada (ver abaixo)

### 🚫 Impedir review duplicada
- Antes de criar uma review, verificar se o usuário já avaliou aquele filme
- Lançar `ConflictException` caso já exista
- Simples: uma query no service + exception já resolve

### 🔒 Logout (blacklist de tokens)
- JWT é stateless por natureza — o token continua válido até expirar mesmo após "logout"
- Solução prática: guardar tokens invalidados em memória (ou Redis futuramente)
  - Criar tabela `InvalidatedTokens` no banco ou usar `IMemoryCache`
  - Middleware verifica se o token está na blacklist antes de autorizar
- **Não é overengineering** — é quase obrigatório para qualquer API com auth real

---

## 🖼️ Upload de imagem para filmes (esforço médio)

Como você roda tudo via Docker Compose, a abordagem ideal é:

1. Salvar imagens em `/app/images` dentro do container
2. Mapear esse caminho para um **volume Docker** no `docker-compose.yml`:
   ```yaml
   volumes:
     - ./images:/app/images
   ```
3. Campo `ImagePath` (string, nullable) no model `Movie`
4. Endpoint usa `IFormFile` opcional no request
5. Retornar a URL relativa no response

> ⚠️ **Não salvar em Base64 no banco** — fica pesado e lento nas queries.
> Quando quiser evoluir para cloud (S3/R2), é só trocar o service de storage.

---

## ❌ Não faça agora — Explicação do porquê

### Cookie HttpOnly para JWT
- **O que é:** Em vez de retornar o token no body, o servidor seta um cookie HttpOnly — o browser manda automaticamente em toda request e JavaScript não consegue ler (mais seguro contra XSS).
- **Por que não agora:** Sua API é consumida pelo Swagger e futuramente por um front React. Cookie complica CORS (`credentials: 'include'` em todo fetch, `AllowCredentials()` no backend). Para mobile ou outros clients, não funciona bem. Refresh token (que você já tem no roadmap) resolve o problema real de segurança de forma mais universal.
- **Conclusão:** Deixa pra quando tiver o front React pronto e souber exatamente como vai autenticar.

---

## 🗺️ Roadmap Completo Revisado (ordem sugerida)

### Fase 1 — Fundação (fazer primeiro)
1.✅  `readonly` em todas as injeções de dependência
2.✅  Mover validações `null` dos controllers para os services
3. Trocar middleware para `IExceptionHandler`
4.✅  Adicionar `ConflictException` (409)
5.✅  Impedir review duplicada do mesmo usuário no mesmo filme
6.✅  Logout com blacklist de tokens (IMemoryCache)

### ✅ Fase 2 — Features
7.✅  Upload de imagem opcional para filmes (volume Docker)
8.✅  Paginação em todos os endpoints de listagem
9. ✅ Padronização de responses (envelope `{ data, message, status }`)
10.✅  Busca de filmes por título e filtros

### Fase 3 — Robustez
11. Testes unitários com xUnit (services primeiro)
12. Melhoria nas mensagens de erro
13. Rate limiting
14. Cache em endpoints de leitura (GET /movies)
15. Refresh token + expiração do token de blacklist

### Fase 4 — Evolução
16. Front-end React
17. Migração de storage local → S3/Cloudflare R2 (quando sair do Docker local)
18. Refactoring contínuo de arquitetura

---

*Gerado em: março de 2026*