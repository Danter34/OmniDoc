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
  type KeyboardEvent as ReactKeyboardEvent,
  type PointerEvent as ReactPointerEvent,
} from "react";

import { ChatInput } from "@/components/chat/chat-input";
import { ChatMessageItem } from "@/components/chat/chat-message-item";
import { getCitationKey } from "@/components/chat/citation-badge";
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
  const [activeCitationKey, setActiveCitationKey] = useState<string | null>(
    null,
  );
  const [selectedDocumentId, setSelectedDocumentId] = useState<string | null>(
    null,
  );
  const [pdfTarget, setPdfTarget] = useState<PdfPageTarget | null>(null);
  const [pdfViewerOpen, setPdfViewerOpen] = useState(false);
  const [pdfPaneWidth, setPdfPaneWidth] = useState(48);
  const [isResizingPdf, setIsResizingPdf] = useState(false);
  const splitAreaRef = useRef<HTMLDivElement>(null);
  const splitterRef = useRef<HTMLDivElement>(null);
  const pdfPaneWidthRef = useRef(48);
  const pendingPdfPaneWidthRef = useRef(48);
  const resizeFrameRef = useRef<number | null>(null);
  const isResizingPdfRef = useRef(false);
  const previousBodyStylesRef = useRef<{
    cursor: string;
    userSelect: string;
  } | null>(null);
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
      setActiveCitationKey(null);
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
    setActiveCitationKey(null);
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
        setActiveCitationKey(null);
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
      if (!fromCitation) {
        setActiveCitationKey(null);
      }
      setPdfViewerOpen(true);
    },
    [],
  );

  const selectCitation = useCallback(
    (citation: Citation, index: number) => {
      setSelectedCitation({ citation, index });
      setActiveCitationKey(getCitationKey(citation));
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

  const setPdfPaneWidthValue = useCallback((value: number) => {
    const nextWidth = Math.min(68, Math.max(30, value));
    pdfPaneWidthRef.current = nextWidth;
    pendingPdfPaneWidthRef.current = nextWidth;
    splitAreaRef.current?.style.setProperty(
      "--pdf-pane-width",
      `${nextWidth}%`,
    );
    splitterRef.current?.setAttribute(
      "aria-valuenow",
      String(Math.round(nextWidth)),
    );
    setPdfPaneWidth(nextWidth);
  }, []);

  const restoreResizeDocumentStyles = useCallback(() => {
    const previousStyles = previousBodyStylesRef.current;
    if (!previousStyles) {
      return;
    }

    document.body.style.cursor = previousStyles.cursor;
    document.body.style.userSelect = previousStyles.userSelect;
    previousBodyStylesRef.current = null;
  }, []);

  const handleSplitterPointerDown = useCallback(
    (event: ReactPointerEvent<HTMLDivElement>) => {
      if (event.button !== 0 || isResizingPdfRef.current) {
        return;
      }

      event.preventDefault();
      event.currentTarget.setPointerCapture(event.pointerId);
      isResizingPdfRef.current = true;
      previousBodyStylesRef.current = {
        cursor: document.body.style.cursor,
        userSelect: document.body.style.userSelect,
      };
      document.body.style.cursor = "col-resize";
      document.body.style.userSelect = "none";
      setIsResizingPdf(true);
    },
    [],
  );

  const handleSplitterPointerMove = useCallback(
    (event: ReactPointerEvent<HTMLDivElement>) => {
      if (!event.currentTarget.hasPointerCapture(event.pointerId)) {
        return;
      }

      const bounds = splitAreaRef.current?.getBoundingClientRect();
      if (!bounds || bounds.width === 0) {
        return;
      }

      pendingPdfPaneWidthRef.current = Math.min(
        68,
        Math.max(30, ((event.clientX - bounds.left) / bounds.width) * 100),
      );

      if (resizeFrameRef.current !== null) {
        return;
      }

      resizeFrameRef.current = window.requestAnimationFrame(() => {
        const nextWidth = pendingPdfPaneWidthRef.current;
        pdfPaneWidthRef.current = nextWidth;
        splitAreaRef.current?.style.setProperty(
          "--pdf-pane-width",
          `${nextWidth}%`,
        );
        splitterRef.current?.setAttribute(
          "aria-valuenow",
          String(Math.round(nextWidth)),
        );
        resizeFrameRef.current = null;
      });
    },
    [],
  );

  const commitSplitterResize = useCallback(() => {
    if (!isResizingPdfRef.current) {
      return;
    }

    isResizingPdfRef.current = false;
    if (resizeFrameRef.current !== null) {
      window.cancelAnimationFrame(resizeFrameRef.current);
      resizeFrameRef.current = null;
    }

    const finalWidth = pendingPdfPaneWidthRef.current;
    pdfPaneWidthRef.current = finalWidth;
    splitAreaRef.current?.style.setProperty(
      "--pdf-pane-width",
      `${finalWidth}%`,
    );
    splitterRef.current?.setAttribute(
      "aria-valuenow",
      String(Math.round(finalWidth)),
    );
    setPdfPaneWidth(finalWidth);
    setIsResizingPdf(false);
    restoreResizeDocumentStyles();
  }, [restoreResizeDocumentStyles]);

  const finishSplitterResize = useCallback(
    (event: ReactPointerEvent<HTMLDivElement>) => {
      if (!isResizingPdfRef.current) {
        return;
      }

      if (event.currentTarget.hasPointerCapture(event.pointerId)) {
        event.currentTarget.releasePointerCapture(event.pointerId);
      }
      commitSplitterResize();
    },
    [commitSplitterResize],
  );

  const handleSplitterKeyDown = useCallback(
    (event: ReactKeyboardEvent<HTMLDivElement>) => {
      let nextWidth = pdfPaneWidthRef.current;

      if (event.key === "ArrowLeft") {
        nextWidth -= 2;
      } else if (event.key === "ArrowRight") {
        nextWidth += 2;
      } else if (event.key === "Home") {
        nextWidth = 30;
      } else if (event.key === "End") {
        nextWidth = 68;
      } else {
        return;
      }

      event.preventDefault();
      setPdfPaneWidthValue(nextWidth);
    },
    [setPdfPaneWidthValue],
  );

  useEffect(
    () => () => {
      if (resizeFrameRef.current !== null) {
        window.cancelAnimationFrame(resizeFrameRef.current);
      }
      isResizingPdfRef.current = false;
      restoreResizeDocumentStyles();
    },
    [restoreResizeDocumentStyles],
  );

  const closeMobileSidebar = useCallback(() => setMobileSidebarOpen(false), []);
  const closeCitationPanel = useCallback(() => setSelectedCitation(null), []);
  const closePdfViewer = useCallback(() => {
    setPdfViewerOpen(false);
    setActiveCitationKey(null);
  }, []);
  const selectDocument = useCallback(
    (documentId: string) => openDocument(documentId),
    [openDocument],
  );

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
      <section className="glass-panel flex h-[calc(100vh-11.5rem)] min-h-[640px] overflow-hidden rounded-2xl">
        <ConversationSidebar
          activeConversationId={activeConversationId}
          conversations={conversations}
          disabled={isStreaming}
          error={conversationsError}
          isLoading={conversationsLoading}
          mobileOpen={mobileSidebarOpen}
          onCreate={createNewConversation}
          onDelete={removeConversation}
          onMobileClose={closeMobileSidebar}
          onSelect={selectConversationAndReset}
        />

        <div
          className="relative flex min-h-0 min-w-0 flex-1"
          ref={splitAreaRef}
          style={
            {
              "--pdf-pane-width": `${pdfPaneWidth}%`,
            } as CSSProperties
          }
        >
          {pdfViewerOpen && selectedDocument ? (
            <aside className="absolute inset-0 z-30 w-full min-w-0 border-r border-line-subtle bg-surface lg:relative lg:z-auto lg:w-[var(--pdf-pane-width)] lg:shrink-0">
              <PdfViewer
                document={selectedDocument}
                documents={documents}
                key={selectedDocument.id}
                onClose={closePdfViewer}
                onDocumentSelect={selectDocument}
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
                "group relative z-10 hidden w-4 shrink-0 touch-none cursor-col-resize items-center justify-center bg-transparent transition-colors hover:bg-splitter-hit-active focus-visible:bg-splitter-hit-active focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-focus-ring lg:flex",
                isResizingPdf && "bg-splitter-hit-active",
              )}
              onDoubleClick={() => setPdfPaneWidthValue(48)}
              onKeyDown={handleSplitterKeyDown}
              onLostPointerCapture={commitSplitterResize}
              onPointerCancel={finishSplitterResize}
              onPointerDown={handleSplitterPointerDown}
              onPointerMove={handleSplitterPointerMove}
              onPointerUp={finishSplitterResize}
              ref={splitterRef}
              role="separator"
              tabIndex={0}
              title="Kéo để thay đổi kích thước · Nhấp đúp để đặt lại"
            >
              <span
                className={cn(
                  "h-12 w-0.5 rounded-full bg-splitter transition-[background-color,box-shadow] group-hover:bg-splitter-active group-hover:shadow-[0_0_12px_var(--splitter-track-active)]",
                  isResizingPdf &&
                    "bg-splitter-active shadow-[0_0_12px_var(--splitter-track-active)]",
                )}
              />
            </div>
          ) : null}

          <div className="flex min-h-0 min-w-0 flex-1 flex-col">
          <header className="flex h-16 shrink-0 items-center gap-3 border-b border-line-subtle bg-surface/65 px-4 backdrop-blur-xl sm:px-5">
            <Button
              aria-label="Mở danh sách hội thoại"
              className="size-9 px-0 lg:hidden"
              onClick={() => setMobileSidebarOpen(true)}
              variant="ghost"
            >
              <Menu className="size-5" />
            </Button>
            <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-info-subtle text-accent shadow-[0_0_16px_var(--sidebar-icon-glow)]">
              <MessageSquareText className="size-4.5" />
            </span>
            <div className="min-w-0 flex-1">
              <h1 className="truncate text-sm font-semibold text-content">
                {activeConversation?.title ?? "Hỏi đáp tài liệu"}
              </h1>
              <p className="mt-0.5 flex items-center gap-1.5 text-xs text-muted">
                <BookOpenCheck className="size-3.5" />
                {indexedDocuments.length} tài liệu sẵn sàng
              </p>
            </div>
            <div className="hidden min-w-0 items-center gap-1.5 md:flex">
              <label className="sr-only" htmlFor="chat-document-selector">
                Chọn tài liệu PDF
              </label>
              <select
                className="h-11 max-w-44 rounded-lg border border-line-subtle bg-surface px-3 text-xs text-content-secondary outline-none transition focus:border-focus-ring focus:ring-2 focus:ring-focus-glow xl:max-w-56"
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
                  closePdfViewer();
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
                <FileText className="size-4 text-accent" />
              ) : (
                <PanelLeftOpen className="size-4" />
              )}
            </Button>
            <span
              className={cn(
                "hidden items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-medium ring-1 ring-inset",
                !pdfViewerOpen && "sm:inline-flex",
                realtimeStatus === "connected"
                  ? "bg-success-subtle text-success ring-success"
                  : "bg-surface-subtle text-muted ring-line-subtle",
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
              className="h-full overflow-y-auto overscroll-contain bg-surface/35"
              onScroll={handleScroll}
              ref={containerRef}
            >
              {!historyReady || (messagesLoading && activeConversationId) ? (
                <div className="flex h-full items-center justify-center">
                  <div className="flex items-center gap-2.5 text-sm text-muted">
                    <Spinner className="size-5 text-accent" />
                    Đang tải lịch sử hội thoại...
                  </div>
                </div>
              ) : messagesError ? (
                <div className="flex h-full items-center justify-center px-6 text-center">
                  <div>
                    <p className="text-sm font-medium text-content">
                      Không thể tải tin nhắn
                    </p>
                    <p className="mt-2 text-sm text-muted">
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
                      activeCitationKey={activeCitationKey}
                      key={message.id}
                      message={message}
                      onCitationSelect={selectCitation}
                    />
                  ))}
                  {streamError ? (
                    <p className="rounded-xl border border-danger bg-danger-subtle px-4 py-3 text-sm text-danger">
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
        onClose={closeCitationPanel}
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
        <span className="mx-auto flex size-16 items-center justify-center rounded-3xl text-brand-icon shadow-[0_0_32px_var(--brand-icon-shadow)] [background-image:var(--gradient-brand)]">
          <Bot className="size-8" />
        </span>
        <p className="mt-6 text-sm font-medium text-accent">
          OmniDoc RAG Assistant
        </p>
        <h2 className="mt-1.5 text-2xl font-semibold tracking-tight text-content">
          Khám phá tri thức trong {workspaceName}
        </h2>
        <p className="mx-auto mt-3 max-w-lg text-sm leading-6 text-muted">
          Đặt câu hỏi để nhận câu trả lời có căn cứ, kèm trích dẫn đến đúng tài
          liệu và số trang.
        </p>

        <div className="mt-7 grid gap-2.5 text-left sm:grid-cols-3">
          {SUGGESTED_PROMPTS.map((prompt) => (
            <button
              className="glass-panel rounded-2xl p-3.5 text-xs leading-5 text-content-secondary transition-[background-color,border-color,color,box-shadow,transform] hover:-translate-y-0.5 hover:border-focus-ring hover:bg-info-subtle hover:text-accent hover:shadow-[var(--accent-glow)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring disabled:cursor-not-allowed disabled:opacity-50"
              disabled={disabled}
              key={prompt}
              onClick={() => onSuggestion(prompt)}
              type="button"
            >
              <Sparkles className="mb-2 size-4 text-accent" />
              {prompt}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
