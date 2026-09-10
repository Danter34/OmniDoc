<p align="center">
  <img src="frontend/public/images/logo-full.png" alt="OmniDoc Logo" width="450" />
</p>

<p align="center">
  <strong>Enterprise Document Intelligence & Semantic RAG Platform</strong><br>
  <em>Nền tảng trí tuệ tài liệu doanh nghiệp và hỏi đáp ngữ nghĩa thời gian thực, độc lập mô hình AI (Model-Agnostic) với hiệu năng xử lý cao.</em>
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

## 📑 Mục lục

- [Tổng quan dự án](#-tổng-quan-dự-án)
- [Tính năng cốt lõi (Core Features)](#-tính-năng-cốt-lõi-core-features)
- [Tech Stack Ma trận](#-tech-stack-ma-trận)
- [Kiến trúc hệ thống & Luồng dữ liệu](#-kiến-trúc-hệ-thống--luồng-dữ-liệu)
  - [1. Ingestion Pipeline (Nạp & Lập chỉ mục tài liệu)](#1-ingestion-pipeline-nạp--lập-chỉ-mục-tài-liệu)
  - [2. RAG Query & Streaming Pipeline (Truy vấn & Trả lời ngữ nghĩa)](#2-rag-query--streaming-pipeline-truy-vấn--trả-lời-ngữ-nghĩa)
  - [3. Cấu trúc thư mục Clean Architecture & App Router](#3-cấu-trúc-thư-mục-clean-architecture--app-router)
- [Bảng ma trận biến môi trường (Environment Variables)](#-bảng-ma-trận-biến-môi-trường-environment-variables)
- [Hướng dẫn Clone & Chạy Local từ A-Z (Quickstart Guide)](#-hướng-dẫn-clone--chạy-local-từ-a-z-quickstart-guide)
  - [Yêu cầu tiên quyết (Prerequisites)](#yêu-cầu-tiên-quyết-prerequisites)
  - [Bước 1: Clone mã nguồn](#bước-1-clone-mã-nguồn)
  - [Bước 2: Chuẩn bị biến môi trường (.env)](#bước-2-chuẩn-bị-biến-môi-trường-env)
  - [Cách 1: Khởi chạy trọn gói bằng Docker Compose (Khuyên dùng)](#cách-1-khởi-chạy-trọn-gói-bằng-docker-compose-khuyên-dùng)
  - [Cách 2: Chế độ Hybrid Development (Chỉnh sửa mã nguồn trực tiếp)](#cách-2-chế-độ-hybrid-development-chỉnh-sửa-mã-nguồn-trực-tiếp)
  - [Danh sách URL truy cập dịch vụ cục bộ](#danh-sách-url-truy-cập-dịch-vụ-cục-bộ)
- [Trải nghiệm hệ thống lần đầu (First-time Walkthrough)](#-trải-nghiệm-hệ-thống-lần-đầu-first-time-walkthrough)
- [Chế độ Showcase & Dữ liệu mẫu (Demo Mode)](#-chế-độ-showcase--dữ-liệu-mẫu-demo-mode)
- [Xử lý sự cố thường gặp ở Local (Troubleshooting)](#-xử-lý-sự-cố-thường-gặp-ở-local-troubleshooting)
- [Đóng góp & Giấy phép](#-đóng-góp--giấy-phép)

---

## 🌟 Tổng quan dự án

**OmniDoc** là giải pháp RAG (Retrieval-Augmented Generation) và quản lý tri thức tài liệu cấp doanh nghiệp được thiết kế theo tiêu chuẩn công nghiệp hiện đại. Hệ thống giải quyết trọn vẹn bài toán chuyển hóa kho tài liệu phi cấu trúc (PDF, Word, Excel, PowerPoint, Markdown, v.v.) thành các tri thức số hóa có khả năng truy vấn tức thì với độ chính xác cao.

Điểm nổi bật của OmniDoc là khả năng **tự động chuẩn hóa tài liệu (Canonical PDF Normalization)**, **phân tích ngữ nghĩa kèm số trang chính xác (Page-accurate Citation)**, và **kiến trúc Model-Agnostic** cho phép doanh nghiệp linh hoạt lựa chọn hoặc thay thế mô hình AI/Embedding (Google Gemini, OpenAI, Anthropic Claude, DeepSeek hoặc Local LLM qua Ollama) mà không bị phụ thuộc vào bất kỳ nhà cung cấp độc quyền nào.

---

## 🚀 Tính năng cốt lõi (Core Features)

### 1. Document Ingestion Pipeline tự động & Đa định dạng
- **Hỗ trợ định dạng phong phú:** PDF (`.pdf`), Microsoft Word (`.docx`), PowerPoint (`.pptx`), Excel (`.xlsx`), CSV (`.csv`), Markdown (`.md`), và Plain Text (`.txt`).
- **Chuẩn hóa thông minh (Canonical PDF):** Sử dụng Gotenberg v8 (với 2 engine chuyên dụng LibreOffice và Chromium headless) để chuyển đổi mọi tài liệu văn phòng về định dạng PDF chuẩn hóa trước khi trích xuất, đảm bảo hiển thị đồng nhất trên trình đọc tài liệu (PDF Viewer).
- **Trích xuất phân trang chi tiết:** Bóc tách text kết hợp tọa độ cấu trúc trang tài liệu thực tế thông qua thư viện `PdfPig`.
- **Recursive Text Chunking:** Thuật toán phân đoạn văn bản đệ quy bảo toàn ngữ cảnh câu/đoạn, kết hợp kỹ thuật overlapping và lưu trữ chính xác số trang (`PageNumber`) cho từng chunk.
- **Tiến trình xử lý thời gian thực (Realtime Ingestion Tracking):** Tự động phát sóng trạng thái xử lý tới giao diện người dùng qua SignalR Hub (`DocumentProgressHub`) qua các bước:
  - `Validating` (5%) $\rightarrow$ `Normalizing` (30%) $\rightarrow$ `Extracting` (40-45%) $\rightarrow$ `Chunking` (50%) $\rightarrow$ `Embedding` (50-90%) $\rightarrow$ `Completed` (100%).
  - Khi có sự cố, hệ thống ghi nhận mã lỗi phân loại (`FailureCode`) và thông báo lỗi rõ ràng.

### 2. Semantic Vector Search với PostgreSQL + pgvector
- **Vector Embeddings 768 chiều:** Tương thích chuẩn với các mô hình Embedding thế hệ mới (tham chiếu mặc định `gemini-embedding-2`).
- **Cosine Distance Search:** Tận dụng toán tử khoảng cách Cosine (`<=>`) của extension `pgvector` ngay trong PostgreSQL để truy vấn các đoạn tài liệu tương đồng nhất (`1 - Distance >= MinSimilarityScore`).
- **Bảo mật phân vùng không gian làm việc (Workspace Isolation):** Toàn bộ vector chunks được gắn chặt với `WorkspaceId`, đảm bảo phân tách tuyệt đối dữ liệu giữa các tenant/tổ chức.

### 3. Real-time AI Chat & Trích dẫn nguồn động (Citations)
- **Streaming mượt mà qua Server-Sent Events (SSE):** API streaming trực tiếp token từ mô hình AI về giao diện người dùng với cấu hình chống đệm (`X-Accel-Buffering: no`), mang lại trải nghiệm phản hồi gõ chữ tức thì.
- **Citation State Machine:** Bộ máy phân tích luồng token (`CitationStreamStateMachine`) phát hiện và ánh xạ tự động các điểm trích dẫn thành các Citation Badges có thể tương tác: hiển thị tên tài liệu gốc, số trang thực tế và đoạn trích dẫn chứng (`Excerpt`).
- **Quản lý hội thoại đa lượt (Multi-turn History):** Lưu trữ lịch sử trao đổi theo phiên làm việc, tự động ghép nối ngữ cảnh trước đó vào RAG prompt để mô hình hiểu rõ luồng đàm thoại.

### 4. Kiến trúc AI Pluggable & Model-Agnostic
- Trừu tượng hóa hoàn toàn tầng AI qua các interface `IChatCompletionService` và `IEmbeddingService`.
- Sẵn sàng tích hợp Microsoft Semantic Kernel và Microsoft.Extensions.AI.
- Hoán đổi linh hoạt qua cấu hình môi trường:
  - **Google Gemini API:** `gemini-3.6-flash` cho Chat và `gemini-embedding-2` cho Embeddings (Cấu hình mặc định).
  - **Local LLM / OpenAI-compatible:** Dễ dàng mở rộng cho Ollama, vLLM, DeepSeek, Claude, GPT-4o.
  - **Chế độ Mock (`AI_PROVIDER=Mock`):** Tích hợp sẵn dịch vụ giả lập offline, cho phép nhà phát triển chạy thử nghiệm toàn bộ hệ sinh thái mà không cần tốn chi phí hay xin cấp API Key bên ngoài.

### 5. Multi-tenant Workspaces & Bảo mật cấp Enterprise
- **Phân quyền vai trò chi tiết (RBAC):** `Owner` (Chủ sở hữu), `Admin` (Quản trị viên), `Member` (Thành viên).
- **Mời thành viên qua Email:** Hệ thống gửi token mời gia nhập (`WorkspaceInvitation`) có thời hạn xác thực an toàn.
- **Xác thực JWT Stateful với TokenVersion:** Mỗi User sở hữu một trường `TokenVersion` trong database. Khi đăng xuất hoặc đổi mật khẩu, `TokenVersion` tăng lên, lập tức vô hiệu hóa toàn bộ token JWT đã phát hành trước đó trên mọi thiết bị.
- **Bảo mật tài khoản:** Quy trình đăng ký yêu cầu OTP xác thực email, cơ chế quên mật khẩu qua liên kết có chữ ký bảo mật (hỗ trợ kiểm tra bằng Mailpit ở local).

---

## 🛠 Tech Stack Ma trận

Hệ thống được phát triển với các phiên bản công nghệ mới nhất, đảm bảo tính ổn định và hiệu năng cao:

| Phân tầng (Layer) | Công nghệ / Thư viện chính | Phiên bản | Vai trò & Mục đích sử dụng |
| :--- | :--- | :--- | :--- |
| **Backend Framework** | .NET (C#) | `10.0` (`net10.0`) | Nền tảng thực thi Web API hiệu năng cao, lập trình bất đồng bộ tối ưu |
| **Kiến trúc ứng dụng** | Clean Architecture + CQRS | MediatR `14.2.0` | Tách biệt ranh giới giữa Domain, Application, Infrastructure và Presentation |
| **ORM & Data Access** | EF Core + Npgsql | `10.0.11` / `10.0.3` | Quản trị cơ sở dữ liệu quan hệ, thực thi migrations tự động |
| **Vector Database** | PostgreSQL + pgvector | PG `17` / pgvector `0.3.0` | Lưu trữ dữ liệu hệ thống và bảng chỉ mục vector embedding 768 chiều |
| **Background Processing** | Hangfire | `1.8.25` | Quản lý tác vụ chạy ngầm phân tán (Document Ingestion, Email Outbox) |
| **Document Processing** | Gotenberg | `8.0` (Docker) | Chuyển đổi DOCX, PPTX, XLSX, CSV, HTML sang Canonical PDF chuẩn qua API |
| **PDF Text Parsing** | PdfPig | `0.1.16` | Bóc tách text và tọa độ trang từ PDF mà không phụ thuộc Adobe SDK |
| **AI / Semantic Engine** | Semantic Kernel / Custom Clients | `1.80.0` | Trừu tượng hóa dịch vụ AI; tích hợp tham chiếu Google Gemini API |
| **Caching & Lock** | Redis | `7-alpine` | Quản lý cache dữ liệu, giới hạn tốc độ (rate limiting) và khóa phân tán |
| **Realtime Gateway** | SignalR + SSE | ASP.NET Core SignalR | Bắn thông báo tiến trình tài liệu và streaming tin nhắn chat thời gian thực |
| **API Docs & Testing** | Scalar + OpenAPI | Scalar `2.17.1` | Sinh tài liệu API tương tác trực quan chuẩn OpenAPI tại `/scalar/v1` |
| **Email Mocking** | Mailpit | `latest` (Docker) | Giả lập máy chủ SMTP cục bộ kèm Web UI xem email OTP tại cổng `8025` |
| **Frontend Framework** | Next.js (App Router) | `16.3.3` | Framework React kết xuất phía máy chủ (SSR/SSG), Server Actions |
| **UI Library & Styling** | React + Tailwind CSS | React `19.2.8` / Tailwind `v4` | Giao diện Responsive hiện đại, tối ưu giao diện sáng/tối (Dark Mode) |
| **Reverse Proxy / Edge** | Nginx | `1.28-alpine` | Cổng điều hướng tập trung, xử lý định tuyến `/api/`, `/hubs/`, `/` và SSE |

---

## 🏗 Kiến trúc hệ thống & Luồng dữ liệu

### 1. Ingestion Pipeline (Nạp & Lập chỉ mục tài liệu)

```
[Người dùng tải tệp lên]
        │ (PDF, Word, Excel, PowerPoint, Text, CSV)
        ▼
[OmniDoc Web API (Upload Controller)]
        │ 1. Lưu tệp gốc vào File Storage (/app/data/documents)
        │ 2. Khởi tạo Document record (Status: Uploaded)
        ▼
[Hangfire Background Queue: DocumentProcessingJob]
        │
        ├─► [Giai đoạn Validating - 5%]
        │   Kiểm tra định dạng, dung lượng và tính toàn vẹn của tệp
        │
        ├─► [Giai đoạn Normalizing - 30%]
        │   Gửi tệp sang Gotenberg (Chromium/LibreOffice Engine)
        │   ──► Sinh ra tệp chuẩn hóa: Canonical PDF
        │
        ├─► [Giai đoạn Extracting - 40~45%]
        │   PdfPig bóc tách toàn bộ nội dung văn bản kèm số trang (PageNumber)
        │
        ├─► [Giai đoạn Chunking - 50%]
        │   RecursiveTextChunker phân đoạn đệ quy theo kích thước và độ gối đầu (Overlap)
        │
        ├─► [Giai đoạn Embedding - 50~90%]
        │   Gửi từng lô (Batch 16 chunks) sang IEmbeddingService (Gemini / Mock)
        │   ──► Nhận về mảng Vector số thực 768 chiều
        │
        └─► [Giai đoạn Completed - 100%]
            Lưu các DocumentChunks vào PostgreSQL (pgvector)
            Cập nhật Document.Status = Indexed
            Bắn sự kiện hoàn tất tới client qua SignalR DocumentProgressHub
```

### 2. RAG Query & Streaming Pipeline (Truy vấn & Trả lời ngữ nghĩa)

```
[Client (Next.js Frontend)]
        │
        │ POST /api/workspaces/{id}/chat/stream (Câu hỏi người dùng)
        ▼
[ChatController (StreamMessageQuery)]
        │
        ├─► 1. Gọi IEmbeddingService sinh vector embedding cho câu hỏi
        │
        ├─► 2. Truy vấn Vector Database (PostgreSQL + pgvector):
        │      Thực thi Cosine Distance: chunk.Embedding <=> QueryVector
        │      Lọc theo: WorkspaceId == currentWorkspaceId
        │      Lấy ra Top-K chunks phù hợp nhất có điểm tương đồng cao
        │
        ├─► 3. RagPromptBuilder:
        │      Ghép nội dung trích xuất (Context Chunks) + Lịch sử hội thoại + Câu hỏi
        │
        ├─► 4. Gọi IChatCompletionService (Gemini / Mock / Local LLM)
        │      Khởi tạo luồng sinh phản hồi (StreamResponseAsync)
        │
        ├─► 5. CitationStreamStateMachine:
        │      Quét token, phát hiện tham chiếu nguồn tài liệu
        │      Chuyển đổi thành các Citation frame (DocumentTitle, PageNumber, Excerpt)
        │
        ▼
[Nginx Edge Proxy] (X-Accel-Buffering: no, Connection: keep-alive)
        │
        ▼
[Client SSE Consumer (useChatStream hook)]
        Render từng từ theo thời gian thực + Hiển thị thẻ trích dẫn tài liệu dẫn chứng
```

### 3. Cấu trúc thư mục Clean Architecture & App Router

```
OmniDoc/
├── backend/                              # Kiến trúc Clean Architecture .NET 10
│   ├── src/
│   │   ├── Core/
│   │   │   ├── OmniDoc.Domain/          # Thực thể lõi (Entities, Enums, Exceptions)
│   │   │   │   ├── Entities/            # Document, DocumentChunk, Workspace, User, v.v.
│   │   │   │   └── Enums/               # DocumentFormat, ProcessingStage, WorkspaceRole
│   │   │   └── OmniDoc.Application/     # Use Cases, CQRS (MediatR), Interfaces, DTOs
│   │   │       ├── Common/              # Interfaces (IEmbeddingService, IChatCompletionService)
│   │   │       └── Features/            # Auth, Chat, Documents, Invitations, Workspaces
│   │   ├── Infrastructure/
│   │   │   ├── OmniDoc.Persistence/     # Entity Framework Core, Migrations, pgvector configs
│   │   │   │   ├── Configurations/      # Fluent API mappings (DocumentChunkConfiguration, v.v.)
│   │   │   │   ├── Contexts/            # ApplicationDbContext
│   │   │   │   └── Migrations/          # Các file EF Core Migration đã tạo
│   │   │   └── OmniDoc.Infrastructure/  # Thực thi dịch vụ ngoại vi
│   │   │       ├── Jobs/                # DocumentProcessingJob, EmailOutboxDispatcher
│   │   │       ├── Services/            # Gotenberg, PdfPig, RecursiveChunker, VectorRetrieval
│   │   │       └── Services/Ai/         # GeminiChatCompletionService, GeminiEmbeddingService
│   │   └── Presentation/
│   │       └── OmniDoc.API/             # REST API Controllers, SignalR Hubs, Middleware
│   │           ├── Controllers/         # AuthController, ChatController, DocumentsController
│   │           ├── Hubs/                # DocumentProgressHub, NotificationHub
│   │           └── Program.cs           # Cấu hình DI, Middleware, Scalar API Docs, Hangfire
│   └── tests/
│       └── OmniDoc.UnitTests/           # Kiểm thử đơn vị cho toàn bộ các layer
│
├── frontend/                             # Giao diện Next.js 16 (App Router) + React 19
│   ├── public/images/                   # Logo, biểu tượng hệ thống (logo-full.png)
│   └── src/
│       ├── app/                         # App Router Pages & Layouts
│       │   ├── (auth)/                  # Đăng nhập, đăng ký, quên/đặt lại mật khẩu
│       │   ├── (dashboard)/             # Quản lý không gian làm việc, tài liệu, cài đặt
│       │   │   └── workspaces/[id]/chat # Giao diện Chat RAG tương tác thời gian thực
│       │   ├── layout.tsx               # Root layout với ThemeProvider & AuthProvider
│       │   └── page.tsx                 # Trang Landing Page giới thiệu giải pháp
│       ├── components/                  # UI Components tách biệt theo module
│       │   ├── auth/                    # Modal OTP, Form đăng nhập/đăng ký
│       │   ├── chat/                    # Khung chat, Citation badges, Markdown renderer
│       │   ├── document/                # Dropzone tải tài liệu, PDF Viewer, Progress badges
│       │   └── workspace/               # Chuyển đổi workspace, quản lý thành viên
│       ├── hooks/                       # Custom hooks (useChatStream, useSignalR, useAuth)
│       └── services/                    # Tầng giao tiếp HTTP API Client
│
├── nginx/                               # Cấu hình Nginx Reverse Proxy (default.conf)
├── tools/                               # Bộ công cụ kiểm thử tự động, dữ liệu mẫu
├── docker-compose.yml                   # Khởi chạy toàn bộ hạ tầng chỉ với một lệnh
├── .env.example                         # File mẫu cấu hình biến môi trường
└── README.md                            # Tài liệu hướng dẫn hệ thống
```

---

## 🔐 Bảng ma trận biến môi trường (Environment Variables)

Hệ thống quản lý cấu hình thông qua file `.env` tại thư mục gốc. Dưới đây là bảng tra cứu chi tiết các biến:

| Tên biến môi trường | Giá trị mặc định | Bắt buộc / Tùy chọn | Giải thích kỹ thuật & Hướng dẫn thiết lập |
| :--- | :--- | :--- | :--- |
| **`COMPOSE_PROJECT_NAME`** | `omnidoc` | Tùy chọn | Tên định danh nhóm container trong Docker Compose. |
| **`APP_URL`** | `http://localhost` | Khuyên dùng | Địa chỉ gốc truy cập ứng dụng từ trình duyệt người dùng. |
| **`POSTGRES_DB`** | `omnidoc_db` | Bắt buộc | Tên cơ sở dữ liệu PostgreSQL. |
| **`POSTGRES_USER`** | `omnidoc` | Bắt buộc | Tài khoản quản trị cơ sở dữ liệu PostgreSQL. |
| **`POSTGRES_PASSWORD`** | *(Trống)* | **Bắt buộc** | Mật khẩu truy cập database (Ví dụ: `OmniDocDevSecret2026!`). |
| **`JWT_SECRET`** | *(Trống)* | **Bắt buộc** | Chuỗi khóa bí mật mã hóa JWT (Tối thiểu 32 ký tự ngẫu nhiên). |
| **`JWT_ISSUER`** | `OmniDocApi` | Tùy chọn | Tên định danh đơn vị phát hành token JWT. |
| **`JWT_AUDIENCE`** | `OmniDocClient` | Tùy chọn | Tên định danh đối tượng người dùng nhận token JWT. |
| **`JWT_EXPIRY_MINUTES`**| `1440` | Tùy chọn | Thời gian sống của JWT token (1440 phút = 24 giờ). |
| **`AI_PROVIDER`** | `Gemini` | **Bắt buộc** | Chọn nhà cung cấp AI: `Gemini` hoặc `Mock` (chạy offline). |
| **`GEMINI_API_KEY`** | *(Trống)* | Bắt buộc nếu dùng Gemini | Khóa API lấy từ Google AI Studio (bỏ trống nếu `AI_PROVIDER=Mock`). |
| **`GEMINI_CHAT_MODEL`** | `gemini-3.6-flash` | Tùy chọn | Tên mô hình ngôn ngữ lớn xử lý hội thoại RAG. |
| **`GEMINI_EMBEDDING_MODEL`**| `gemini-embedding-2` | Tùy chọn | Mô hình sinh vector nhúng 768 chiều cho tài liệu. |
| **`GOTENBERG_BASE_URL`**| `http://gotenberg:3000`| Tùy chọn | Địa chỉ mạng nội bộ tới dịch vụ chuyển đổi tài liệu Gotenberg. |
| **`SMTP_HOST`** | `mailpit` | Tùy chọn | Địa chỉ máy chủ gửi email (`mailpit` trong Docker, `localhost` nếu chạy ngoài). |
| **`SMTP_PORT`** | `1025` | Tùy chọn | Cổng kết nối SMTP của Mailpit để bắt email nội bộ. |
| **`EMAIL_SHOW_DEMO_OTP`**| `false` | Tùy chọn | Đặt `true` nếu muốn API trả trực tiếp mã OTP trong phản hồi HTTP để test nhanh. |
| **`SHOWCASE_ENABLED`** | `false` | Tùy chọn | Bật chế độ Demo Showcase cho khách trải nghiệm. |
| **`SHOWCASE_SEED_ON_STARTUP`**| `false` | Tùy chọn | Tự động nạp tài liệu mẫu và tài khoản demo khi khởi động API. |
| **`NEXT_PUBLIC_SHOWCASE_ENABLED`**| `false` | Tùy chọn | Cờ phía giao diện người dùng hiển thị nút đăng nhập demo nhanh. |

> [!TIP]
> **Cách lấy Google Gemini API Key hoàn toàn miễn phí:**
> 1. Truy cập [Google AI Studio](https://aistudio.google.com/).
> 2. Đăng nhập bằng tài khoản Google cá nhân và nhấn **Get API Key**.
> 3. Tạo một key mới và dán vào trường `GEMINI_API_KEY` trong file `.env`.
> 
> *Nếu bạn chỉ muốn chạy thử nghiệm tính năng mà không có kết nối internet hoặc không muốn sử dụng API key, hãy đặt `AI_PROVIDER=Mock`.*

---

## 💻 Hướng dẫn Clone & Chạy Local từ A-Z (Quickstart Guide)

### Yêu cầu tiên quyết (Prerequisites)
Đảm bảo máy tính cá nhân của bạn đã cài đặt các công cụ sau:
- **Git:** Để clone mã nguồn ([Tải Git](https://git-scm.com/)).
- **Docker & Docker Desktop:** Yêu cầu Docker Engine hỗ trợ Compose v2 ([Tải Docker Desktop](https://www.docker.com/products/docker-desktop/)). Đảm bảo Docker Desktop đang chạy.
- *(Tùy chọn dành riêng cho Cách 2 - Hybrid Dev):*
  - **.NET 10 SDK** ([Tải .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0))
  - **Node.js 20+** và **npm** ([Tải Node.js](https://nodejs.org/))

---

### Bước 1: Clone mã nguồn

Mở terminal (PowerShell, Bash hoặc Command Prompt) và thực hiện:

```bash
git clone https://github.com/Danter34/OmniDoc.git
cd OmniDoc
```

---

### Bước 2: Chuẩn bị biến môi trường (.env)

Tạo file cấu hình `.env` từ file mẫu `.env.example`:

**Trên Linux / macOS:**
```bash
cp .env.example .env
```

**Trên Windows (PowerShell):**
```powershell
Copy-Item .env.example .env
```

Mở file `.env` vừa tạo bằng trình soạn thảo bất kỳ và cập nhật tối thiểu các giá trị sau:
```ini
# Đặt mật khẩu tùy ý cho database
POSTGRES_PASSWORD=OmniDocPassword2026!

# Sinh một chuỗi ngẫu nhiên tối thiểu 32 ký tự làm JWT Secret
JWT_SECRET=super-secret-key-with-at-least-32-characters-long!

# Cấu hình AI: Điền Gemini API Key hoặc chọn chế độ Mock
AI_PROVIDER=Gemini
GEMINI_API_KEY=AIzaSyYourActualGeminiApiKeyHere...
# (Nếu không có key, đổi thành: AI_PROVIDER=Mock)
```

---

### Cách 1: Khởi chạy trọn gói bằng Docker Compose (Khuyên dùng)

Đây là phương thức đơn giản, tin cậy nhất vì toàn bộ các dịch vụ (PostgreSQL pgvector, Redis, Gotenberg, Mailpit, .NET Backend, Next.js Frontend, Nginx) đều được đóng gói chuẩn hóa trong container.

Thực hiện đúng quy trình 3 bước sau:

#### 1. Khởi động các dịch vụ hạ tầng phụ trợ:
```bash
docker compose up -d db redis gotenberg mailpit
```
*Lệnh này sẽ tải các image cần thiết và khởi động cơ sở dữ liệu cùng các dịch vụ nền.*

#### 2. Khởi tạo schema cơ sở dữ liệu qua Migration:
```bash
docker compose run --rm --no-deps backend --migrate-only=true
```
*Lệnh này chạy một container backend tạm thời để tự động thực thi các migration của EF Core lên PostgreSQL (kích hoạt pgvector extension và tạo toàn bộ bảng).*

#### 3. Khởi chạy toàn bộ hệ thống ứng dụng:
```bash
docker compose up -d --build
```
*Docker Compose sẽ tiến hành build image backend, image frontend và khởi chạy Nginx reverse proxy tại cổng 80.*

> [!NOTE]
> Để theo dõi trạng thái khởi động của toàn bộ dịch vụ, bạn có thể chạy:
> ```bash
> docker compose ps
> ```
> Khi thấy các container đều ở trạng thái `healthy` hoặc `running`, hệ thống đã sẵn sàng đón nhận kết nối.

---

### Cách 2: Chế độ Hybrid Development (Chỉnh sửa mã nguồn trực tiếp)

Dành cho lập trình viên muốn phát triển tính năng, debug hoặc sửa code Backend / Frontend trực tiếp trên máy host.

#### Bước 2.1: Chạy hạ tầng phụ trợ bằng Docker
Khởi chạy Database, Redis, Gotenberg và Mailpit:
```bash
docker compose up -d db redis gotenberg mailpit
```

#### Bước 2.2: Khởi chạy Backend (.NET 10 API)
1. Mở cửa sổ Terminal thứ nhất:
   ```bash
   cd backend
   ```
2. Cập nhật cơ sở dữ liệu:
   ```bash
   dotnet ef database update --project src/Infrastructure/OmniDoc.Persistence --startup-project src/Presentation/OmniDoc.API
   ```
3. Chạy ứng dụng API:
   ```bash
   dotnet run --project src/Presentation/OmniDoc.API
   ```
   *Backend API sẽ lắng nghe tại `http://localhost:5151` (và `https://localhost:7157`). Tài liệu tương tác Scalar API Docs sẵn sàng tại `http://localhost:5151/scalar/v1`.*

#### Bước 2.3: Khởi chạy Frontend (Next.js)
1. Mở cửa sổ Terminal thứ hai:
   ```bash
   cd frontend
   ```
2. Cài đặt các gói phụ thuộc:
   ```bash
   npm install
   ```
3. Chạy môi trường phát triển:
   ```bash
   npm run dev
   ```
   *Frontend sẽ khởi chạy tại `http://localhost:3000` và tự động kết nối với backend tại `http://localhost:5151`.*

---

### 🌐 Danh sách URL truy cập dịch vụ cục bộ

| Dịch vụ | Chạy qua Docker Compose (Cách 1) | Chạy Hybrid Dev (Cách 2) | Mục đích & Tài khoản mẫu |
| :--- | :--- | :--- | :--- |
| **Web UI (Frontend)** | `http://localhost` (Port 80) | `http://localhost:3000` | Giao diện người dùng chính của OmniDoc |
| **API Health Check** | `http://localhost/api/health` | `http://localhost:5151/api/health` | Kiểm tra trạng thái sống của Backend API |
| **Interactive API Docs**| `http://localhost/scalar/v1` | `http://localhost:5151/scalar/v1` | Xem và thử nghiệm các API qua giao diện Scalar |
| **Hangfire Dashboard** | *(Chỉ bật ở môi trường Dev)* | `http://localhost:5151/hangfire` | Bảng điều khiển quản lý Background Jobs |
| **Mailpit Web UI** | `http://localhost:8025` | `http://localhost:8025` | Hộp thư giả lập để đọc email chứa **mã OTP đăng ký** |
| **PostgreSQL Port** | `localhost:5432` | `localhost:5432` | Kết nối CSDL (`User: omnidoc`, `DB: omnidoc_db`) |

---

## 🎯 Trải nghiệm hệ thống lần đầu (First-time Walkthrough)

Sau khi khởi chạy thành công, bạn có thể thực hiện theo các bước sau để trải nghiệm hệ thống:

1. **Mở trình duyệt:** Truy cập `http://localhost` (hoặc `http://localhost:3000` nếu chạy Hybrid).
2. **Đăng ký tài khoản:**
   - Nhấn **Bắt đầu ngay** hoặc **Đăng ký**.
   - Nhập thông tin (Tên, Email, Mật khẩu tối thiểu 8 ký tự kèm chữ hoa, số và ký tự đặc biệt).
   - Hệ thống sẽ gửi một mã OTP gồm 6 chữ số để xác thực tài khoản.
3. **Lấy mã OTP xác thực email:**
   - Mở một tab mới truy cập **Mailpit Web UI**: `http://localhost:8025`.
   - Mở email mới nhất do OmniDoc gửi đến và copy mã OTP 6 chữ số.
   - Quay lại trang đăng ký của OmniDoc, nhập mã OTP để kích hoạt tài khoản.
4. **Tạo Không gian làm việc (Workspace):**
   - Đặt tên cho Workspace mới (Ví dụ: *Dự Án Nghiên Cứu AI*).
5. **Tải tài liệu lên:**
   - Kéo thả các tệp PDF, Word hoặc Markdown vào vùng tải tệp.
   - Quan sát thanh tiến trình cập nhật theo thời gian thực (Validating $\rightarrow$ Normalizing $\rightarrow$ Extracting $\rightarrow$ Chunking $\rightarrow$ Embedding $\rightarrow$ Hoàn tất).
6. **Hỏi đáp với AI (Semantic RAG Chat):**
   - Chuyển sang tab **Trò chuyện (Chat)**.
   - Đặt các câu hỏi liên quan đến nội dung tài liệu vừa tải lên.
   - Trải nghiệm tốc độ gõ chữ streaming và bấm vào các thẻ trích dẫn (Citations) để đối chiếu nguồn tài liệu kèm số trang gốc!

---

## 🎪 Chế độ Showcase & Dữ liệu mẫu (Demo Mode)

OmniDoc được tích hợp sẵn một bộ dữ liệu mẫu thực tế về tài chính kinh tế khu vực (*Báo cáo Ổn định Tài chính ASEAN+3 2024 - AMRO*) cùng các vector embedding đã tính toán sẵn. Nếu bạn muốn mở tài khoản Demo mà không cần đăng ký hay tải tài liệu thủ công:

1. Trong file `.env`, cấu hình các cờ sau:
   ```ini
   SHOWCASE_ENABLED=true
   SHOWCASE_SEED_ON_STARTUP=true
   NEXT_PUBLIC_SHOWCASE_ENABLED=true
   ```
2. Khởi động lại hệ thống:
   ```bash
   docker compose up -d --build
   ```
3. Truy cập trang đăng nhập, bạn sẽ thấy nút **Trải nghiệm nhanh với tài khoản Demo**:
   - **Email:** `guest@omnidoc.io`
   - **Mật khẩu:** `OmniDoc-Showcase2026!`
   - Ngay sau khi đăng nhập, bạn sẽ có sẵn một Workspace với tài liệu được lập chỉ mục hoàn chỉnh để trò chuyện ngay lập tức.

---

## 🔧 Xử lý sự cố thường gặp ở Local (Troubleshooting)

### 1. Lỗi xung đột cổng (Port Conflict)
- **Triệu chứng:** Container không thể bind vào cổng `5432`, `80` hoặc `8025`.
- **Nguyên nhân:** Máy tính của bạn đã có một dịch vụ PostgreSQL hoặc web server cục bộ (IIS, Apache, Skype) đang chiếm dụng cổng này.
- **Cách khắc phục:**
  - *Kiểm tra cổng đang bị chiếm (Windows PowerShell):*
    ```powershell
    Get-NetTCPConnection -LocalPort 5432, 80, 8025 -ErrorAction SilentlyContinue
    ```
  - Tạm dừng dịch vụ PostgreSQL cục bộ trên Windows Services (`services.msc`) hoặc đổi cổng ánh xạ trong file `docker-compose.yml` (Ví dụ đổi `"80:80"` thành `"8088:80"`).

### 2. Lỗi Quota / Rate-limit khi gọi Gemini API (HTTP 429)
- **Triệu chứng:** Tài liệu bị báo lỗi ở giai đoạn `Embedding` hoặc tin nhắn chat báo lỗi nhà cung cấp AI.
- **Nguyên nhân:** Khóa API Gemini miễn phí chạm ngưỡng giới hạn số lượt gọi/phút (RPM).
- **Cách khắc phục:**
  - Tạm thời chuyển sang chế độ giả lập bằng cách sửa trong `.env`:
    ```ini
    AI_PROVIDER=Mock
    ```
  - Khởi động lại backend: `docker compose restart backend`. Hệ thống sẽ tiếp tục hoạt động trơn tru với các câu trả lời giả lập.

### 3. Không nhận được email mã OTP đăng ký
- **Nguyên nhân:** Bạn chưa mở Mailpit hoặc cấu hình sai SMTP.
- **Cách khắc phục:**
  - Truy cập hộp thư nội bộ Mailpit tại `http://localhost:8025`. Toàn bộ email gửi đi từ OmniDoc đều được giữ lại tại đây để kiểm tra.
  - Ngoài ra, bạn có thể bật biến `EMAIL_SHOW_DEMO_OTP=true` trong file `.env` để API trả thẳng mã OTP về giao diện khi đăng ký.

### 4. Cách theo dõi logs chi tiết của các dịch vụ
Khi gặp bất kỳ vấn đề bất thường nào, hãy xem log chi tiết của từng container bằng lệnh:
```bash
# Xem log Backend API
docker compose logs -f backend

# Xem log Frontend Next.js
docker compose logs -f frontend

# Xem log xử lý tài liệu Gotenberg
docker compose logs -f gotenberg

# Xem log Cơ sở dữ liệu PostgreSQL
docker compose logs -f db
```

### 5. Dọn dẹp và làm mới toàn bộ môi trường (Full Reset)
Nếu bạn muốn xóa toàn bộ dữ liệu, database volumes và khởi động lại từ trạng thái nguyên bản sạch sẽ nhất:
```bash
# Dừng và xóa sạch các volume dữ liệu
docker compose down -v

# Thực hiện lại quy trình khởi tạo từ Bước 1
```

---

## 🤝 Đóng góp & Giấy phép

### Hướng dẫn đóng góp (Contributing)
Mọi đóng góp nhằm tối ưu hóa hiệu năng, bổ sung Provider AI mới hoặc hoàn thiện giao diện đều được hoan nghênh:
1. Fork repository về tài khoản cá nhân.
2. Tạo một nhánh tính năng mới (`git checkout -b feature/amazing-feature`).
3. Commit các thay đổi (`git commit -m 'feat: add amazing new feature'`).
4. Đẩy lên nhánh của bạn (`git push origin feature/amazing-feature`).
5. Tạo một **Pull Request** giải thích rõ nội dung cập nhật.

### Giấy phép (License)
Dự án được phân phối dưới giấy phép **MIT License**. Vui lòng xem thông tin chi tiết tại file `LICENSE` trong kho mã nguồn.

---

<p align="center">
  Phát triển với ❤️ bởi đội ngũ OmniDoc. Chúc bạn có trải nghiệm tuyệt vời với nền tảng trí tuệ tài liệu doanh nghiệp!
</p>