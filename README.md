<p align="center">
  <img src="frontend/public/images/logo-full.png" alt="OmniDoc Logo" width="350" />
</p>

<p align="center">
  <strong>Enterprise Document Intelligence & Semantic RAG Platform</strong><br>
  <em>A RAG platform for enterprises: upload documents, build semantic indexes, and chat/Q&A in real time.</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Next.js-16.3-black?style=for-the-badge&logo=nextdotjs&logoColor=white" alt="Next.js" />
  <img src="https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react&logoColor=black" alt="React 19" />
  <img src="https://img.shields.io/badge/PostgreSQL-17-336791?style=for-the-badge&logo=postgresql&logoColor=white" alt="PostgreSQL 17" />
  <img src="https://img.shields.io/badge/pgvector-Supported-336791?style=for-the-badge&logo=postgresql&logoColor=white" alt="pgvector" />
  <img src="https://img.shields.io/badge/Redis-7.0-DC382D?style=for-the-badge&logo=redis&logoColor=white" alt="Redis 7" />
  <img src="https://img.shields.io/badge/Docker-Enabled-2496ED?style=for-the-badge&logo=docker&logoColor=white" alt="Docker" />
  <img src="https://img.shields.io/badge/Tailwind_CSS-v4-06B6D4?style=for-the-badge&logo=tailwindcss&logoColor=white" alt="Tailwind CSS v4" />
</p>

---

## 📑 Table of Contents

- [Project Overview](#-project-overview)
- [Core Features](#-core-features)
- [Tech Stack Matrix](#-tech-stack-matrix)
- [System Architecture & Data Flow](#-system-architecture--data-flow)
  - [1. Ingestion Pipeline (Document Loading & Indexing)](#1-ingestion-pipeline-document-loading--indexing)
  - [2. RAG Query & Streaming Pipeline (Semantic Query & Answer)](#2-rag-query--streaming-pipeline-semantic-query--answer)
  - [3. Clean Architecture & App Router Folder Structure](#3-clean-architecture--app-router-folder-structure)
- [Environment Variables Matrix](#-environment-variables-matrix)
- [Quickstart Guide](#-quickstart-guide)
  - [Prerequisites](#prerequisites)
  - [Step 1: Clone the Source Code](#step-1-clone-the-source-code)
  - [Step 2: Prepare Environment Variables (.env)](#step-2-prepare-environment-variables-env)
  - [Option 1: Launch Everything with Docker Compose (Recommended)](#option-1-launch-everything-with-docker-compose-recommended)
  - [Option 2: Hybrid Development Mode (Edit Source Code Directly)](#option-2-hybrid-development-mode-edit-source-code-directly)
  - [List of Local Service URLs](#-list-of-local-service-urls)
- [First-time Walkthrough](#-first-time-walkthrough)
- [Showcase Mode & Sample Data (Demo Mode)](#-showcase-mode--sample-data-demo-mode)
- [Common Local Troubleshooting](#-common-local-troubleshooting)

---

## 🌟 Project Overview

**OmniDoc** is a self-hosted RAG (Retrieval-Augmented Generation) platform that solves the problem of uploading multi-format documents, extracting their content, building semantic indexes, and chatting/asking questions directly with an AI in real time based on the context of that document.

Users create a Workspace and upload documents (PDF, Word, Excel, Slides...). The system automatically extracts the text, splits it into chunks, computes semantic vectors, and stores them in the database. When a user asks a question, the system finds the most relevant excerpts, feeds them into the LLM's context, then streams the answer back to the UI in real time along with source citations (page numbers, passages).

Two notable points: the system automatically normalizes every document format into a Canonical PDF before extraction (so citations always match the original page numbers correctly), and the AI layer is abstracted behind an interface, allowing you to switch between Google Gemini, OpenAI, Anthropic Claude, DeepSeek, or a locally-run LLM via Ollama without touching any business logic code.

---

## 🚀 Core Features

### 1. Automatic, Multi-format Document Ingestion Pipeline
- **Supported formats:** PDF (`.pdf`), Word (`.docx`), PowerPoint (`.pptx`), Excel (`.xlsx`), CSV (`.csv`), Markdown (`.md`), Plain Text (`.txt`).
- **Normalization via Gotenberg:** Gotenberg v8 (using LibreOffice and headless Chromium engines) converts every office document into a normalized PDF before extraction, so the PDF Viewer displays content consistently regardless of the original format.
- **Page-aware extraction:** `PdfPig` extracts text along with the actual page coordinates, without depending on the Adobe SDK.
- **Recursive Text Chunking:** Recursively segments text, preserving sentence/paragraph context, with overlap between chunks, and stores the correct `PageNumber` for each chunk.
- **Real-time progress tracking:** A SignalR Hub (`DocumentProgressHub`) pushes processing status to the UI through the following stages:
  - `Validating` (5%) → `Normalizing` (30%) → `Extracting` (40–45%) → `Chunking` (50%) → `Embedding` (50–90%) → `Completed` (100%).
  - On error, the system records a `FailureCode` with a specific message, making debugging easier than just reporting a generic "failure."

### 2. Semantic Vector Search with PostgreSQL + pgvector
- **768-dimensional vector embeddings**, defaulting to `gemini-embedding-2`.
- **Cosine Distance Search:** Uses pgvector's `<=>` operator directly in PostgreSQL, filtering by `1 - Distance >= MinSimilarityScore` to retrieve the semantically closest chunks — no separate vector database required.
- **Workspace isolation:** Every chunk is tied to a `WorkspaceId`, so data between tenants never mixes.

### 3. Real-time AI Chat with Source Citations
- **Streaming via SSE:** The API streams tokens directly to the UI with buffering disabled (`X-Accel-Buffering: no`), so text appears as soon as the model generates it, without stuttering.
- **Citation State Machine:** `CitationStreamStateMachine` scans the token stream, detects citation points, and turns them into clickable badges — showing the document name, page number, and original excerpt.
- **Multi-turn conversation history:** Stored per session, automatically appending previous turns into the RAG prompt so the model doesn't "forget" earlier questions.

### 4. Pluggable AI Architecture, No Vendor Lock-in
- The AI layer is abstracted through the `IChatCompletionService` and `IEmbeddingService` interfaces.
- Compatible out of the box with Microsoft Semantic Kernel and Microsoft.Extensions.AI.
- Switch providers purely via environment variables, no code changes required:
  - **Google Gemini API:** `gemini-3.6-flash` for chat, `gemini-embedding-2` for embeddings (default).
  - **Local LLM / OpenAI-compatible:** Ollama, vLLM, DeepSeek, Claude, GPT-4o.
  - **Mock mode (`AI_PROVIDER=Mock`):** A simulated offline service — run the entire system without needing an API key or incurring any model call costs.

### 5. Multi-tenant Workspaces & Security
- **RBAC:** `Owner`, `Admin`, `Member`.
- **Invite members via email:** Invitation tokens (`WorkspaceInvitation`) with an expiration.
- **Stateful JWT via TokenVersion:** Each user has a `TokenVersion` field. Logging out or changing a password increments this value, immediately invalidating all old tokens (across every device) — no need to wait for the JWT to naturally expire.
- **Account security:** Registration requires email OTP verification; password reset via a signed link (verifiable through Mailpit locally).

---

## 🛠 Tech Stack Matrix

| Layer | Technology / Main Library | Version | Role & Purpose |
| :--- | :--- | :--- | :--- |
| **Backend Framework** | .NET (C#) | `10.0` (`net10.0`) | Web API execution platform, asynchronous programming |
| **Application Architecture** | Clean Architecture + CQRS | MediatR `14.2.0` | Separates boundaries between Domain, Application, Infrastructure, and Presentation |
| **ORM & Data Access** | EF Core + Npgsql | `10.0.11` / `10.0.3` | Manages the relational database, runs automatic migrations |
| **Vector Database** | PostgreSQL + pgvector | PG `17` / pgvector `0.3.0` | Stores system data and the 768-dimensional vector embedding index table |
| **Background Processing** | Hangfire | `1.8.25` | Manages distributed background jobs (Document Ingestion, Email Outbox) |
| **Document Processing** | Gotenberg | `8.0` (Docker) | Converts DOCX, PPTX, XLSX, CSV, HTML into a Canonical PDF via API |
| **PDF Text Parsing** | PdfPig | `0.1.16` | Extracts text and page coordinates from PDFs, without depending on the Adobe SDK |
| **AI / Semantic Engine** | Semantic Kernel / Custom Clients | `1.80.0` | Abstracts the AI layer; integrates with Google Gemini API by default |
| **Caching & Lock** | Redis | `7-alpine` | Data caching, rate limiting, and distributed locking |
| **Realtime Gateway** | SignalR + SSE | ASP.NET Core SignalR | Pushes document processing progress and streams chat messages |
| **API Docs & Testing** | Scalar + OpenAPI | Scalar `2.17.1` | Interactive OpenAPI-compliant API documentation at `/scalar/v1` |
| **Email Mocking** | Mailpit | `latest` (Docker) | Simulated SMTP with a Web UI for viewing OTP emails, on port `8025` |
| **Frontend Framework** | Next.js (App Router) | `16.3.3` | React SSR/SSG, Server Actions |
| **UI Library & Styling** | React + Tailwind CSS | React `19.2.8` / Tailwind `v4` | Responsive UI, dark mode |
| **Reverse Proxy / Edge** | Nginx | `1.28-alpine` | Routes `/api/`, `/hubs/`, `/` and handles SSE |

---

## 🏗 System Architecture & Data Flow

### 1. Ingestion Pipeline (Document Loading & Indexing)

```
[User uploads a file]
        │ (PDF, Word, Excel, PowerPoint, Text, CSV)
        ▼
[OmniDoc Web API (Upload Controller)]
        │ 1. Save the original file to File Storage (/app/data/documents)
        │ 2. Initialize a Document record (Status: Uploaded)
        ▼
[Hangfire Background Queue: DocumentProcessingJob]
        │
        ├─► [Validating stage - 5%]
        │   Checks the file format, size, and integrity
        │
        ├─► [Normalizing stage - 30%]
        │   Sends the file to Gotenberg (Chromium/LibreOffice Engine)
        │   ──► Produces a normalized file: Canonical PDF
        │
        ├─► [Extracting stage - 40~45%]
        │   PdfPig extracts all text content along with page numbers (PageNumber)
        │
        ├─► [Chunking stage - 50%]
        │   RecursiveTextChunker recursively segments text by size and overlap
        │
        ├─► [Embedding stage - 50~90%]
        │   Sends each batch (16 chunks) to IEmbeddingService (Gemini / Mock)
        │   ──► Receives back a 768-dimensional floating-point vector array
        │
        └─► [Completed stage - 100%]
            Saves the DocumentChunks to PostgreSQL (pgvector)
            Updates Document.Status = Indexed
            Fires a completion event to the client via SignalR DocumentProgressHub
```

### 2. RAG Query & Streaming Pipeline (Semantic Query & Answer)

```
[Client (Next.js Frontend)]
        │
        │ POST /api/workspaces/{id}/chat/stream (User's question)
        ▼
[ChatController (StreamMessageQuery)]
        │
        ├─► 1. Calls IEmbeddingService to generate a vector embedding for the question
        │
        ├─► 2. Queries the Vector Database (PostgreSQL + pgvector):
        │      Executes a Cosine Distance query: chunk.Embedding <=> QueryVector
        │      Filters by: WorkspaceId == currentWorkspaceId
        │      Retrieves the Top-K most relevant chunks with the highest similarity score
        │
        ├─► 3. RagPromptBuilder:
        │      Combines the extracted content (Context Chunks) + conversation history + the question
        │
        ├─► 4. Calls IChatCompletionService (Gemini / Mock / Local LLM)
        │      Initiates the response generation stream (StreamResponseAsync)
        │
        ├─► 5. CitationStreamStateMachine:
        │      Scans tokens, detects source document references
        │      Converts them into Citation frames (DocumentTitle, PageNumber, Excerpt)
        │
        ▼
[Nginx Edge Proxy] (X-Accel-Buffering: no, Connection: keep-alive)
        │
        ▼
[Client SSE Consumer (useChatStream hook)]
        Renders each word in real time + displays supporting document citation badges
```

### 3. Clean Architecture & App Router Folder Structure

```
OmniDoc/
├── backend/                              # .NET 10 Clean Architecture
│   ├── src/
│   │   ├── Core/
│   │   │   ├── OmniDoc.Domain/          # Core entities (Entities, Enums, Exceptions)
│   │   │   │   ├── Entities/            # Document, DocumentChunk, Workspace, User, etc.
│   │   │   │   └── Enums/               # DocumentFormat, ProcessingStage, WorkspaceRole
│   │   │   └── OmniDoc.Application/     # Use Cases, CQRS (MediatR), Interfaces, DTOs
│   │   │       ├── Common/              # Interfaces (IEmbeddingService, IChatCompletionService)
│   │   │       └── Features/            # Auth, Chat, Documents, Invitations, Workspaces
│   │   ├── Infrastructure/
│   │   │   ├── OmniDoc.Persistence/     # Entity Framework Core, Migrations, pgvector configs
│   │   │   │   ├── Configurations/      # Fluent API mappings (DocumentChunkConfiguration, etc.)
│   │   │   │   ├── Contexts/            # ApplicationDbContext
│   │   │   │   └── Migrations/          # Generated EF Core Migration files
│   │   │   └── OmniDoc.Infrastructure/  # External service implementations
│   │   │       ├── Jobs/                # DocumentProcessingJob, EmailOutboxDispatcher
│   │   │       ├── Services/            # Gotenberg, PdfPig, RecursiveChunker, VectorRetrieval
│   │   │       └── Services/Ai/         # GeminiChatCompletionService, GeminiEmbeddingService
│   │   └── Presentation/
│   │       └── OmniDoc.API/             # REST API Controllers, SignalR Hubs, Middleware
│   │           ├── Controllers/         # AuthController, ChatController, DocumentsController
│   │           ├── Hubs/                # DocumentProgressHub, NotificationHub
│   │           └── Program.cs           # DI configuration, Middleware, Scalar API Docs, Hangfire
│   └── tests/
│       └── OmniDoc.UnitTests/           # Unit tests covering every layer
│
├── frontend/                             # Next.js 16 (App Router) + React 19 UI
│   ├── public/images/                   # Logo, system icons (logo-full.png)
│   └── src/
│       ├── app/                         # App Router Pages & Layouts
│       │   ├── (auth)/                  # Login, register, forgot/reset password
│       │   ├── (dashboard)/             # Workspace, document, and settings management
│       │   │   └── workspaces/[id]/chat # Real-time interactive RAG chat interface
│       │   ├── layout.tsx               # Root layout with ThemeProvider & AuthProvider
│       │   └── page.tsx                 # Landing page introducing the solution
│       ├── components/                  # UI components separated by module
│       │   ├── auth/                    # OTP modal, login/register forms
│       │   ├── chat/                    # Chat window, citation badges, Markdown renderer
│       │   ├── document/                # Document upload dropzone, PDF Viewer, progress badges
│       │   └── workspace/               # Workspace switcher, member management
│       ├── hooks/                       # Custom hooks (useChatStream, useSignalR, useAuth)
│       └── services/                    # HTTP API client layer
│
├── nginx/                               # Nginx Reverse Proxy configuration (default.conf)
├── tools/                               # Automated testing toolkit, sample data
├── docker-compose.yml                   # Launches the entire infrastructure with a single command
├── .env.example                         # Sample environment variable configuration file
└── README.md                            # System documentation
```

---

## 🔐 Environment Variables Matrix

Configuration lives in the `.env` file at the project root.

| Environment Variable | Default Value | Required / Optional | Technical Explanation & Setup Guide |
| :--- | :--- | :--- | :--- |
| **`COMPOSE_PROJECT_NAME`** | `omnidoc` | Optional | Container group name in Docker Compose. |
| **`APP_URL`** | `http://localhost` | Recommended | The root address for accessing the app from a browser. |
| **`POSTGRES_DB`** | `omnidoc_db` | Required | PostgreSQL database name. |
| **`POSTGRES_USER`** | `omnidoc` | Required | Database admin account. |
| **`POSTGRES_PASSWORD`** | *(empty)* | **Required** | Database password (e.g., `OmniDocDevSecret2026!`). |
| **`JWT_SECRET`** | *(empty)* | **Required** | Secret key used to sign the JWT (at least 32 random characters). |
| **`JWT_ISSUER`** | `OmniDocApi` | Optional | Name of the JWT issuing authority. |
| **`JWT_AUDIENCE`** | `OmniDocClient` | Optional | Name of the JWT's intended audience. |
| **`JWT_EXPIRY_MINUTES`**| `1440` | Optional | JWT lifetime (1440 minutes = 24 hours). |
| **`AI_PROVIDER`** | `Gemini` | **Required** | Selects the AI provider: `Gemini` or `Mock` (offline). |
| **`GEMINI_API_KEY`** | *(empty)* | Required if using Gemini | Obtained from Google AI Studio (leave blank if `AI_PROVIDER=Mock`). |
| **`GEMINI_CHAT_MODEL`** | `gemini-3.6-flash` | Optional | Model used for RAG conversation handling. |
| **`GEMINI_EMBEDDING_MODEL`**| `gemini-embedding-2` | Optional | Model used to generate 768-dimensional embeddings. |
| **`GOTENBERG_BASE_URL`**| `http://gotenberg:3000`| Optional | Internal address of the Gotenberg service. |
| **`SMTP_HOST`** | `mailpit` | Optional | Mail server (`mailpit` in Docker, `localhost` if running externally). |
| **`SMTP_PORT`** | `1025` | Optional | Mailpit's SMTP port. |
| **`EMAIL_SHOW_DEMO_OTP`**| `false` | Optional | Set to `true` to have the API return the OTP code directly in the response, for quick testing. |
| **`SHOWCASE_ENABLED`** | `false` | Optional | Enables Demo Showcase mode. |
| **`SHOWCASE_SEED_ON_STARTUP`**| `false` | Optional | Automatically loads sample documents and demo accounts on API startup. |
| **`NEXT_PUBLIC_SHOWCASE_ENABLED`**| `false` | Optional | Frontend flag to show the quick demo login button. |

> [!TIP]
> **Get a free Google Gemini API Key:**
> 1. Go to [Google AI Studio](https://aistudio.google.com/).
> 2. Sign in with your Google account and click **Get API Key**.
> 3. Create a new key and paste it into `GEMINI_API_KEY` in `.env`.
>
> *Don't want to request an API key, or have no internet access? Just set `AI_PROVIDER=Mock` and you're ready to go.*

---

## 💻 (Quickstart Guide)

### Prerequisites
- **Git** ([Download Git](https://git-scm.com/)).
- **Docker & Docker Desktop:** requires a Docker Engine that supports Compose v2 ([Download Docker Desktop](https://www.docker.com/products/docker-desktop/)). Make sure Docker Desktop is running.
- *(Only needed for Option 2 - Hybrid Dev):*
  - **.NET 10 SDK** ([Download .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0))
  - **Node.js 20+** and **npm** ([Download Node.js](https://nodejs.org/))

---

### Step 1: Clone the Source Code

```bash
git clone https://github.com/Danter34/OmniDoc.git
cd OmniDoc
```

---

### Step 2: Prepare Environment Variables (.env)

Create a `.env` file from the `.env.example` template:

**On Linux / macOS:**
```bash
cp .env.example .env
```

**On Windows (PowerShell):**
```powershell
Copy-Item .env.example .env
```

Open `.env` and update at minimum:
```ini
# Set any password for the database
POSTGRES_PASSWORD=OmniDocPassword2026!

# Generate a random string of at least 32 characters as the JWT Secret
JWT_SECRET=super-secret-key-with-at-least-32-characters-long!

# AI configuration: Fill in the Gemini API Key or choose Mock mode
AI_PROVIDER=Gemini
GEMINI_API_KEY=AIzaSyYourActualGeminiApiKeyHere...
# (If you don't have a key, change it to: AI_PROVIDER=Mock)
```

---

### Option 1: Launch Everything with Docker Compose (Recommended)

The simplest option — every service (PostgreSQL pgvector, Redis, Gotenberg, Mailpit, .NET backend, Next.js frontend, Nginx) runs in a container.

#### 1. Start the infrastructure services:
```bash
docker compose up -d db redis gotenberg mailpit
```

#### 2. Initialize the database schema via migration:
```bash
docker compose run --rm --no-deps backend --migrate-only=true
```
*Runs a temporary backend container so EF Core can execute the migration against PostgreSQL (enabling the pgvector extension and creating the tables).*

#### 3. Launch the entire system:
```bash
docker compose up -d --build
```
*Builds the backend image, the frontend image, and starts Nginx on port 80.*

> [!NOTE]
> Check startup status:
> ```bash
> docker compose ps
> ```
> Once all containers show `healthy` or `running`, the system is ready.

---

### Option 2: Hybrid Development Mode (Edit Source Code Directly)

For anyone who wants to debug or edit the Backend / Frontend code directly on their machine.

#### Step 2.1: Run supporting infrastructure via Docker
```bash
docker compose up -d db redis gotenberg mailpit
```

#### Step 2.2: Launch the Backend (.NET 10 API)
1. First terminal:
   ```bash
   cd backend
   ```
2. Update the database:
   ```bash
   dotnet ef database update --project src/Infrastructure/OmniDoc.Persistence --startup-project src/Presentation/OmniDoc.API
   ```
3. Run the API:
   ```bash
   dotnet run --project src/Presentation/OmniDoc.API
   ```
   *The backend listens on `http://localhost:5151` (and `https://localhost:7157`). Scalar API Docs are available at `http://localhost:5151/scalar/v1`.*

#### Step 2.3: Launch the Frontend (Next.js)
1. Second terminal:
   ```bash
   cd frontend
   ```
2. Install dependencies:
   ```bash
   npm install
   ```
3. Run the dev server:
   ```bash
   npm run dev
   ```
   *The frontend runs at `http://localhost:3000` and automatically connects to the backend at `http://localhost:5151`.*

---

### 🌐 List of Local Service URLs

| Service | Docker Compose (Option 1) | Hybrid Dev (Option 2) | Purpose & Sample Account |
| :--- | :--- | :--- | :--- |
| **Web UI (Frontend)** | `http://localhost` (Port 80) | `http://localhost:3000` | OmniDoc's main interface |
| **API Health Check** | `http://localhost/api/health` | `http://localhost:5151/api/health` | Checks whether the Backend API is alive |
| **Interactive API Docs**| `http://localhost/scalar/v1` | `http://localhost:5151/scalar/v1` | Try out the API via the Scalar interface |
| **Hangfire Dashboard** | *(Dev mode only)* | `http://localhost:5151/hangfire` | Manage background jobs |
| **Mailpit Web UI** | `http://localhost:8025` | `http://localhost:8025` | Simulated inbox, for reading **registration OTP codes** |
| **PostgreSQL Port** | `localhost:5432` | `localhost:5432` | Database connection (`User: omnidoc`, `DB: omnidoc_db`) |

---

## 🎯 First-time Walkthrough

1. **Open your browser:** go to `http://localhost` (or `http://localhost:3000` if running in Hybrid mode).
2. **Register an account:**
   - Click **Get Started** or **Sign Up**.
   - Enter your Name, Email, and Password (at least 8 characters, with an uppercase letter, a number, and a special character).
   - The system sends a 6-digit OTP code to verify your email.
3. **Get the OTP code:**
   - Open a new tab and go to the **Mailpit Web UI**: `http://localhost:8025`.
   - Open the latest email from OmniDoc and copy the OTP code.
   - Return to the registration page and enter the code to activate your account.
4. **Create a Workspace:**
   - Give it a name (e.g., *AI Research Project*).
5. **Upload a document:**
   - Drag and drop a PDF, Word, or Markdown file into the upload area.
   - Watch the real-time progress (Validating → Normalizing → Extracting → Chunking → Embedding → Completed).
6. **Chat with the AI:**
   - Switch to the **Chat** tab.
   - Ask questions related to the content you just uploaded.
   - Watch the answer being typed out in real time, and click on the citation badges to cross-reference the source along with the original page number.

---

## 🎪 Showcase Mode & Sample Data (Demo Mode)

OmniDoc comes with a built-in sample regional finance dataset (*ASEAN+3 Financial Stability Report 2024 - AMRO*) along with pre-computed vector embeddings, so you can try out the system without registering or uploading your own documents:

1. In `.env`, enable the following flags:
   ```ini
   SHOWCASE_ENABLED=true
   SHOWCASE_SEED_ON_STARTUP=true
   NEXT_PUBLIC_SHOWCASE_ENABLED=true
   ```
2. Restart:
   ```bash
   docker compose up -d --build
   ```
3. The login page will show a **Try the Quick Demo Account** button:
   - **Email:** `guest@omnidoc.io`
   - **Password:** `OmniDoc-Showcase2026!`
   - Once logged in, you'll immediately have a Workspace with an already-indexed document, ready to chat with.

---

## 🔧 Common Local Troubleshooting

### 1. Port Conflict Errors
- **Symptom:** A container fails to bind to port `5432`, `80`, or `8025`.
- **Cause:** Your machine already has PostgreSQL or another web server (IIS, Apache, Skype) occupying that port.
- **Fix:**
  - Check which process is using the port (Windows PowerShell):
    ```powershell
    Get-NetTCPConnection -LocalPort 5432, 80, 8025 -ErrorAction SilentlyContinue
    ```
  - Temporarily stop the local PostgreSQL service (`services.msc`) or change the port mapping in `docker-compose.yml` (e.g., `"80:80"` → `"8088:80"`).

### 2. Gemini API Quota / Rate-limit Errors (HTTP 429)
- **Symptom:** A document fails at the `Embedding` stage, or the chat reports an error from the AI provider.
- **Cause:** The free Gemini API key hit its calls-per-minute limit.
- **Fix:**
  - Temporarily switch to Mock mode in `.env`:
    ```ini
    AI_PROVIDER=Mock
    ```
  - Restart the backend: `docker compose restart backend`. The system will continue running with simulated responses.

### 3. Not Receiving the Registration OTP Email
- **Cause:** Mailpit isn't open, or the SMTP configuration is wrong.
- **Fix:**
  - Go to `http://localhost:8025` — every email OmniDoc sends lands here.
  - Or enable `EMAIL_SHOW_DEMO_OTP=true` in `.env` so the API returns the OTP code directly in the UI.

### 4. View Detailed Logs
```bash
# Backend API logs
docker compose logs -f backend

# Next.js Frontend logs
docker compose logs -f frontend

# Gotenberg logs
docker compose logs -f gotenberg

# PostgreSQL logs
docker compose logs -f db
```

### 5. Reset the Entire Environment
Wipe all data and volumes, and start fresh:
```bash
# Stop and remove all data volumes
docker compose down -v

# Then start over from Step 1
```
