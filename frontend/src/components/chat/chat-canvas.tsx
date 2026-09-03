"use client";

import {
  ArrowDown,
  BookOpenCheck,
  Bot,
  FileText,
  Menu,
  MessageSquareText,
  PanelLeftOpen,
  Radio,
  Sparkles,
} from "lucide-react";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
} from "react";

import { ChatInput } from "@/components/chat/chat-input";
import { ChatMessageItem } from "@/components/chat/chat-message-item";
import {
  CitationPanel,
  type SelectedCitation,
} from "@/components/chat/citation-panel";
import { ConversationSidebar } from "@/components/chat/conversation-sidebar";
import {
  PdfViewer,
  type PdfPageTarget,
} from "@/components/document/PdfViewer";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { useChatStream } from "@/hooks/use-chat-stream";
import { useConversations } from "@/hooks/use-conversations";
import { useDocumentProgress } from "@/hooks/use-document-progress";
import { useDocuments } from "@/hooks/use-documents";
import { useSmartAutoScroll } from "@/hooks/use-smart-auto-scroll";
import { cn } from "@/lib/utils";
import { getErrorMessage } from "@/services/api-client";
import { conversationService } from "@/services/conversation.service";
import type { Citation } from "@/types/chat.types";
import type { Workspace } from "@/types/workspace.types";

const SUGGESTED_PROMPTS = [
  "Tóm tắt những điểm chính trong các tài liệu.",
  "So sánh các nội dung quan trọng và chỉ ra điểm khác biệt.",
  "Liệt kê các kết luận có trích dẫn nguồn.",
];

export function ChatCanvas({ workspace }: { workspace: Workspace }) {
  const {
    conversations,
    activeConversation,
    activeConversationId,
    isLoading: conversationsLoading,
    error: conversationsError,
    selectConversation,
    createConversation,
    deleteConversation,
    refreshConversations,
  } = useConversations(workspace.id);
  const {
    documents,
    isLoading: documentsLoading,
    applyProgressUpdates,
  } = useDocuments(workspace.id);
  const realtimeStatus = useDocumentProgress(
    workspace.id,
    applyProgressUpdates,
  );
  const [input, setInput] = useState("");
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  const [selectedCitation, setSelectedCitation] =
    useState<SelectedCitation | null>(null);
  const [selectedDocumentId, setSelectedDocumentId] = useState<string | null>(
    null,
  );
  const [pdfTarget, setPdfTarget] = useState<PdfPageTarget | null>(null);
  const [pdfViewerOpen, setPdfViewerOpen] = useState(false);
  const [pdfPaneWidth, setPdfPaneWidth] = useState(48);
  const [isResizingPdf, setIsResizingPdf] = useState(false);
  const splitAreaRef = useRef<HTMLDivElement>(null);
  const navigationRequestIdRef = useRef(0);
  const [loadedConversationId, setLoadedConversationId] = useState<
    string | null
  >(null);
  const [messagesLoading, setMessagesLoading] = useState(true);
  const [messagesError, setMessagesError] = useState<string | null>(null);

  const handleConversationResolved = useCallback(
    (conversationId: string) => {
      setLoadedConversationId(conversationId);
      selectConversation(conversationId);
      void refreshConversations();
    },
    [refreshConversations, selectConversation],
  );

  const handleStreamSettled = useCallback(() => {
    void refreshConversations();
  }, [refreshConversations]);

  const {
    messages,
    isStreaming,
    error: streamError,
    sendMessage,
    stopGenerating,
    replaceMessages,
  } = useChatStream({
    workspaceId: workspace.id,
    conversationId: activeConversationId,
    onConversationResolved: handleConversationResolved,
    onSettled: handleStreamSettled,
  });

  useEffect(() => {
    if (!activeConversationId || isStreaming) {
      return;
    }

    const controller = new AbortController();

    conversationService
      .getMessages(workspace.id, activeConversationId, controller.signal)
      .then((items) => {
        replaceMessages(items);
        setLoadedConversationId(activeConversationId);
        setMessagesError(null);
      })
      .catch((requestError: unknown) => {
        if (
          requestError instanceof DOMException &&
          requestError.name === "AbortError"
        ) {
          return;
        }

        setMessagesError(getErrorMessage(requestError));
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setMessagesLoading(false);
        }
      });

    return () => controller.abort();
  }, [
    activeConversationId,
    isStreaming,
    replaceMessages,
    workspace.id,
  ]);

  const indexedDocuments = useMemo(
    () => documents.filter((document) => document.status === "Indexed"),
    [documents],
  );
  const selectedDocument = useMemo(
    () =>
      documents.find((document) => document.id === selectedDocumentId) ?? null,
    [documents, selectedDocumentId],
  );
  const hasIndexedDocuments = indexedDocuments.length > 0;
  const lastMessage = messages.at(-1);
  const scrollTrigger = `${activeConversationId ?? "new"}:${messages.length}:${
    lastMessage?.content.length ?? 0
  }:${lastMessage?.citations.length ?? 0}:${isStreaming}`;
  const {
    containerRef,
    isPinnedToBottom,
    handleScroll,
    scrollToBottom,
  } = useSmartAutoScroll(scrollTrigger);
  const historyReady =
    !activeConversationId ||
    loadedConversationId === activeConversationId ||
    isStreaming;

  const selectConversationAndReset = useCallback(
    (conversationId: string) => {
      if (isStreaming) {
        return;
      }

      setMessagesLoading(true);
      setMessagesError(null);
      setLoadedConversationId(null);
      setSelectedCitation(null);
      selectConversation(conversationId);
      scrollToBottom("auto");
    },
    [isStreaming, scrollToBottom, selectConversation],
  );

  const createNewConversation = useCallback(async () => {
    if (isStreaming) {
      return;
    }

    const created = await createConversation("Cuộc trò chuyện mới");
    setMessagesLoading(true);
    setMessagesError(null);
    setLoadedConversationId(null);
    setSelectedCitation(null);
    selectConversation(created.id);
    scrollToBottom("auto");
  }, [
    createConversation,
    isStreaming,
    scrollToBottom,
    selectConversation,
  ]);

  const removeConversation = useCallback(
    async (conversationId: string) => {
      if (isStreaming) {
        return;
      }

      await deleteConversation(conversationId);

      if (conversationId === activeConversationId) {
        setLoadedConversationId(null);
        setMessagesLoading(true);
        setMessagesError(null);
        setSelectedCitation(null);
      }
    },
    [activeConversationId, deleteConversation, isStreaming],
  );

  const submitMessage = useCallback(() => {
    const message = input.trim();

    if (!message || !hasIndexedDocuments || isStreaming) {
      return;
    }

    setInput("");
    setMessagesLoading(false);
    setLoadedConversationId(activeConversationId);
    void sendMessage(message);
  }, [
    activeConversationId,
    hasIndexedDocuments,
    input,
    isStreaming,
    sendMessage,
  ]);

  const openDocument = useCallback(
    (documentId: string, pageNumber = 1, fromCitation = false) => {
      navigationRequestIdRef.current += 1;
      setSelectedDocumentId(documentId);
      setPdfTarget({
        pageNumber: Math.max(1, pageNumber),
        requestId: navigationRequestIdRef.current,
        fromCitation,
      });
      setPdfViewerOpen(true);
    },
    [],
  );

  const selectCitation = useCallback(
    (citation: Citation, index: number) => {
      setSelectedCitation({ citation, index });
      openDocument(citation.documentId, citation.pageNumber, true);
    },
    [openDocument],
  );

  const viewCitationInDocument = useCallback(
    (citation: Citation) => {
      openDocument(citation.documentId, citation.pageNumber, true);
      setSelectedCitation(null);
    },
    [openDocument],
  );

  useEffect(() => {
    if (!isResizingPdf) {
      return;
    }

    const previousCursor = document.body.style.cursor;
    const previousUserSelect = document.body.style.userSelect;
    document.body.style.cursor = "col-resize";
    document.body.style.userSelect = "none";

    const handleMouseMove = (event: MouseEvent) => {
      const bounds = splitAreaRef.current?.getBoundingClientRect();

      if (!bounds) {
        return;
      }

      const nextWidth = ((event.clientX - bounds.left) / bounds.width) * 100;
      setPdfPaneWidth(Math.min(68, Math.max(30, nextWidth)));
    };
    const stopResizing = () => setIsResizingPdf(false);

    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", stopResizing);

    return () => {
      document.body.style.cursor = previousCursor;
      document.body.style.userSelect = previousUserSelect;
      window.removeEventListener("mousemove", handleMouseMove);
      window.removeEventListener("mouseup", stopResizing);
    };
  }, [isResizingPdf]);

  const historyUnavailable = Boolean(
    activeConversationId && (!historyReady || messagesLoading),
  );
  const inputDisabled =
    documentsLoading ||
    conversationsLoading ||
    historyUnavailable ||
    !hasIndexedDocuments;
  const disabledReason = documentsLoading
    ? "Đang kiểm tra tài liệu trong Workspace..."
    : conversationsLoading || historyUnavailable
      ? "Đang tải dữ liệu hội thoại..."
    : !hasIndexedDocuments
      ? "Cần ít nhất một tài liệu ở trạng thái Đã lập chỉ mục"
      : undefined;

  return (
    <>
      <section className="grid h-[calc(100vh-11.5rem)] min-h-[640px] overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm lg:grid-cols-[18rem_minmax(0,1fr)]">
        <ConversationSidebar
          activeConversationId={activeConversationId}
          conversations={conversations}
          disabled={isStreaming}
          error={conversationsError}
          isLoading={conversationsLoading}
          mobileOpen={mobileSidebarOpen}
          onCreate={createNewConversation}
          onDelete={removeConversation}
          onMobileClose={() => setMobileSidebarOpen(false)}
          onSelect={selectConversationAndReset}
        />

        <div
          className="relative flex min-h-0 min-w-0"
          ref={splitAreaRef}
          style={
            {
              "--pdf-pane-width": `${pdfPaneWidth}%`,
            } as CSSProperties
          }
        >
          {pdfViewerOpen && selectedDocument ? (
            <aside className="absolute inset-0 z-30 w-full min-w-0 border-r border-slate-200 bg-white lg:relative lg:z-auto lg:w-[var(--pdf-pane-width)] lg:shrink-0">
              <PdfViewer
                document={selectedDocument}
                documents={documents}
                key={selectedDocument.id}
                onClose={() => setPdfViewerOpen(false)}
                onDocumentSelect={(documentId) => openDocument(documentId)}
                target={pdfTarget}
                workspaceId={workspace.id}
              />
            </aside>
          ) : null}

          {pdfViewerOpen && selectedDocument ? (
            <div
              aria-label="Thay đổi độ rộng trình xem PDF"
              aria-orientation="vertical"
              aria-valuemax={68}
              aria-valuemin={30}
              aria-valuenow={Math.round(pdfPaneWidth)}
              className={cn(
                "group relative z-10 hidden w-2 shrink-0 cursor-col-resize items-center justify-center bg-slate-100 transition-colors hover:bg-blue-100 focus-visible:bg-blue-100 focus-visible:outline-none lg:flex",
                isResizingPdf && "bg-blue-100",
              )}
              onDoubleClick={() => setPdfPaneWidth(48)}
              onKeyDown={(event) => {
                if (event.key === "ArrowLeft") {
                  event.preventDefault();
                  setPdfPaneWidth((value) => Math.max(30, value - 2));
                } else if (event.key === "ArrowRight") {
                  event.preventDefault();
                  setPdfPaneWidth((value) => Math.min(68, value + 2));
                }
              }}
              onMouseDown={() => setIsResizingPdf(true)}
              role="separator"
              tabIndex={0}
              title="Kéo để thay đổi kích thước · Nhấp đúp để đặt lại"
            >
              <span className="h-10 w-0.5 rounded-full bg-slate-300 transition group-hover:bg-blue-400" />
            </div>
          ) : null}

          <div className="flex min-h-0 min-w-0 flex-1 flex-col">
          <header className="flex h-16 shrink-0 items-center gap-3 border-b border-slate-200 px-4 sm:px-5">
            <Button
              aria-label="Mở danh sách hội thoại"
              className="size-9 px-0 lg:hidden"
              onClick={() => setMobileSidebarOpen(true)}
              variant="ghost"
            >
              <Menu className="size-5" />
            </Button>
            <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
              <MessageSquareText className="size-4.5" />
            </span>
            <div className="min-w-0 flex-1">
              <h1 className="truncate text-sm font-semibold text-slate-900">
                {activeConversation?.title ?? "Hỏi đáp tài liệu"}
              </h1>
              <p className="mt-0.5 flex items-center gap-1.5 text-xs text-slate-500">
                <BookOpenCheck className="size-3.5" />
                {indexedDocuments.length} tài liệu sẵn sàng
              </p>
            </div>
            <div className="hidden min-w-0 items-center gap-1.5 md:flex">
              <label className="sr-only" htmlFor="chat-document-selector">
                Chọn tài liệu PDF
              </label>
              <select
                className="h-9 max-w-44 rounded-lg border border-slate-200 bg-white pl-3 pr-2 text-xs text-slate-600 outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-500/10 xl:max-w-56"
                disabled={documentsLoading || documents.length === 0}
                id="chat-document-selector"
                onChange={(event) => {
                  if (event.target.value) {
                    openDocument(event.target.value);
                  }
                }}
                value={selectedDocumentId ?? ""}
              >
                <option value="">Chọn tài liệu PDF</option>
                {documents.map((document) => (
                  <option key={document.id} value={document.id}>
                    {document.title || document.fileName}
                  </option>
                ))}
              </select>
            </div>
            <Button
              aria-label={pdfViewerOpen ? "Đóng trình xem PDF" : "Mở trình xem PDF"}
              className="size-9 shrink-0 px-0"
              disabled={documentsLoading || documents.length === 0}
              onClick={() => {
                if (pdfViewerOpen) {
                  setPdfViewerOpen(false);
                  return;
                }

                const documentId = selectedDocumentId ?? documents[0]?.id;
                if (documentId) {
                  openDocument(documentId, pdfTarget?.pageNumber ?? 1);
                }
              }}
              title={pdfViewerOpen ? "Đóng trình xem PDF" : "Mở trình xem PDF"}
              variant="secondary"
            >
              {pdfViewerOpen ? (
                <FileText className="size-4 text-blue-600" />
              ) : (
                <PanelLeftOpen className="size-4" />
              )}
            </Button>
            <span
              className={cn(
                "hidden items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-medium ring-1 ring-inset",
                !pdfViewerOpen && "sm:inline-flex",
                realtimeStatus === "connected"
                  ? "bg-emerald-50 text-emerald-700 ring-emerald-200"
                  : "bg-slate-100 text-slate-500 ring-slate-200",
              )}
            >
              <Radio
                className={cn(
                  "size-3",
                  realtimeStatus === "connected" && "animate-pulse",
                )}
              />
              Tài liệu realtime
            </span>
          </header>

          <div className="relative min-h-0 flex-1">
            <div
              className="h-full overflow-y-auto overscroll-contain bg-slate-50/40"
              onScroll={handleScroll}
              ref={containerRef}
            >
              {!historyReady || (messagesLoading && activeConversationId) ? (
                <div className="flex h-full items-center justify-center">
                  <div className="flex items-center gap-2.5 text-sm text-slate-500">
                    <Spinner className="size-5 text-blue-600" />
                    Đang tải lịch sử hội thoại...
                  </div>
                </div>
              ) : messagesError ? (
                <div className="flex h-full items-center justify-center px-6 text-center">
                  <div>
                    <p className="text-sm font-medium text-slate-800">
                      Không thể tải tin nhắn
                    </p>
                    <p className="mt-2 text-sm text-slate-500">
                      {messagesError}
                    </p>
                  </div>
                </div>
              ) : messages.length === 0 ? (
                <EmptyChatState
                  disabled={inputDisabled}
                  onSuggestion={setInput}
                  workspaceName={workspace.name}
                />
              ) : (
                <div className="mx-auto max-w-4xl space-y-6 px-4 py-6 sm:px-6 sm:py-8">
                  {messages.map((message) => (
                    <ChatMessageItem
                      key={message.id}
                      message={message}
                      onCitationSelect={selectCitation}
                    />
                  ))}
                  {streamError ? (
                    <p className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
                      {streamError}
                    </p>
                  ) : null}
                </div>
              )}
            </div>

            {!isPinnedToBottom ? (
              <Button
                className="absolute bottom-4 left-1/2 -translate-x-1/2 rounded-full shadow-lg"
                icon={<ArrowDown className="size-4" />}
                onClick={() => scrollToBottom()}
                size="sm"
                variant="secondary"
              >
                Cuộn xuống mới nhất
              </Button>
            ) : null}
          </div>

          <ChatInput
            disabled={inputDisabled}
            disabledReason={disabledReason}
            isStreaming={isStreaming}
            onChange={setInput}
            onSend={submitMessage}
            onStop={stopGenerating}
            value={input}
          />
          </div>
        </div>
      </section>

      <CitationPanel
        onClose={() => setSelectedCitation(null)}
        onViewInDocument={viewCitationInDocument}
        selected={selectedCitation}
      />
    </>
  );
}

function EmptyChatState({
  workspaceName,
  disabled,
  onSuggestion,
}: {
  workspaceName: string;
  disabled: boolean;
  onSuggestion: (prompt: string) => void;
}) {
  return (
    <div className="flex min-h-full items-center justify-center px-5 py-10">
      <div className="w-full max-w-2xl text-center">
        <span className="mx-auto flex size-16 items-center justify-center rounded-3xl bg-gradient-to-br from-blue-600 to-indigo-600 text-white shadow-lg shadow-blue-600/20">
          <Bot className="size-8" />
        </span>
        <p className="mt-6 text-sm font-medium text-blue-600">
          OmniDoc RAG Assistant
        </p>
        <h2 className="mt-1.5 text-2xl font-semibold tracking-tight text-slate-950">
          Khám phá tri thức trong {workspaceName}
        </h2>
        <p className="mx-auto mt-3 max-w-lg text-sm leading-6 text-slate-500">
          Đặt câu hỏi để nhận câu trả lời có căn cứ, kèm trích dẫn đến đúng tài
          liệu và số trang.
        </p>

        <div className="mt-7 grid gap-2.5 text-left sm:grid-cols-3">
          {SUGGESTED_PROMPTS.map((prompt) => (
            <button
              className="rounded-2xl border border-slate-200 bg-white p-3.5 text-xs leading-5 text-slate-600 shadow-sm transition hover:border-blue-200 hover:bg-blue-50 hover:text-blue-700 disabled:cursor-not-allowed disabled:opacity-50"
              disabled={disabled}
              key={prompt}
              onClick={() => onSuggestion(prompt)}
              type="button"
            >
              <Sparkles className="mb-2 size-4 text-blue-500" />
              {prompt}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
