# 🎬 MovieHub API

API REST para gerenciamento de filmes, reviews e favoritos, com autenticação JWT e controle de acesso por roles.

---

## 🚀 Tecnologias

- **.NET 8** — framework principal
- **ASP.NET Core** — web API
- **Entity Framework Core** — ORM
- **PostgreSQL** — banco de dados
- **ASP.NET Identity** — gerenciamento de usuários e roles
- **JWT Bearer** — autenticação e autorização
- **AutoMapper** — mapeamento de objetos
- **Swagger / OpenAPI** — documentação e testes da API
- **Docker + Docker Compose** — containerização

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

### Roles
| Role | Permissões |
|------|-----------|
| **Admin** | CRUD completo de filmes, deletar qualquer review |
| **User** | Criar/editar/deletar próprias reviews, gerenciar favoritos |

---

## 📋 Endpoints

### 🔑 Auth
| Método | Rota | Descrição | Auth |
|--------|------|-----------|------|
| POST | `/api/auth/register` | Registrar novo usuário | ❌ |
| POST | `/api/auth/login` | Login e geração de token | ❌ |

### 🎬 Movies
| Método | Rota | Descrição | Auth |
|--------|------|-----------|------|
| GET | `/api/movies` | Listar filmes (paginado) | ❌ |
| GET | `/api/movies/{id}` | Buscar filme por ID | ❌ |
| POST | `/api/movies` | Criar filme | 🔒 Admin |
| PUT | `/api/movies/{id}` | Atualizar filme | 🔒 Admin |
| DELETE | `/api/movies/{id}` | Deletar filme | 🔒 Admin |

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

## 🗂️ Estrutura do Projeto

```
MovieHub/
├── Controllers/          # Endpoints da API
├── Data/
│   └── Dtos/             # Objetos de transferência de dados
│       ├── Auth/
│       ├── Favorite/
│       ├── Movie/
│       ├── Review/
│       └── User/
├── Exceptions/           # Exceptions customizadas
├── Extensions/           # Extension methods
├── Middleware/           # Middleware global de exceções
├── Migrations/           # Migrations do banco de dados
├── Models/               # Entidades do domínio
├── Pagination/           # Classes de paginação
├── Profiles/             # Perfis do AutoMapper
├── Properties/           # Configurações de launch
├── Queries/              # Parâmetros de query (filtros/paginação)
│   └── Movies/
└── Services/
    ├── Implementations/  # Implementações dos services
    └── Interfaces/       # Contratos dos services
```

---

## 🔧 Variáveis de Ambiente

Configure no `docker-compose.yml` ou `appsettings.json`:

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

## 🛣️ Roadmap

Melhorias planejadas para versões futuras:

- [ ] Testes unitários com xUnit
- [ ] Paginação em todos os endpoints de listagem
- [ ] Padronização de responses (envelope padrão com `data`, `message`, `status`)
- [ ] Melhoria no tratamento e descrição dos erros
- [ ] Rate limiting
- [ ] Cache em endpoints de leitura
- [ ] Busca de filmes por título e outros filtros
- [ ] Mais segurança (refresh token, blacklist de tokens)
- [ ] Front-end (React provalmente)
- [ ] Melhoria contínua de código e arquitetura

---

## 👨‍💻 Autor

Feito por **Guilherme Andreis**  
[LinkedIn](https://www.linkedin.com/in/guilherme-boeira-andreis) · [GitHub](https://github.com/gui-andreis)