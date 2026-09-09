"use client";

import {
  ArrowDown,
  BookOpenCheck,
  FileText,
  Menu,
  MessageSquareText,
  PanelLeftOpen,
  Radio,
  Sparkles,
} from "lucide-react";
import Image from "next/image";
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
import { ShowcaseBanner } from "@/components/chat/showcase-banner";
import {
  PdfViewer,
  type PdfPageTarget,
} from "@/components/document/PdfViewer";
import { Button } from "@/components/ui/button";
import { BrandName } from "@/components/ui/logo";
import { Spinner } from "@/components/ui/spinner";
import { useChatStream } from "@/hooks/use-chat-stream";
import { useConversations } from "@/hooks/use-conversations";
import { useDocumentProgress } from "@/hooks/use-document-progress";
import { useDocuments } from "@/hooks/use-documents";
import { useSmartAutoScroll } from "@/hooks/use-smart-auto-scroll";
import { useShowcase } from "@/hooks/use-showcase";
import { SHOWCASE_DEFAULT_DOCUMENT, SHOWCASE_PROMPTS } from "@/lib/showcase";
import { cn } from "@/lib/utils";
import { getErrorMessage } from "@/services/api-client";
import { conversationService } from "@/services/conversation.service";
import type { Citation } from "@/types/chat.types";
import type { Workspace } from "@/types/workspace.types";

const ONBOARDING_HINTS = [
  "Tải lên tài liệu đầu tiên (PDF, Word) để bắt đầu đối soát tri thức.",
  "OmniDoc bảo đảm trích dẫn chính xác kèm trang tham chiếu nguồn.",
];

export function ChatCanvas({ workspace }: { workspace: Workspace }) {
  const { isShowcaseWorkspace: isShowcase } = useShowcase(workspace.id);
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
    updateConversationTitle,
  } = useConversations(workspace.id, !isShowcase);
  const {
    documents,
    isLoading: documentsLoading,
    error: documentsError,
    applyProgressUpdates,
  } = useDocuments(workspace.id);
  const suggestedPrompts = useMemo(() => {
    if (isShowcase) return SHOWCASE_PROMPTS;

    const latestDoc = documents.reduce<(typeof documents)[number] | undefined>(
      (latest, document) =>
        !latest || Date.parse(document.createdAtUtc) > Date.parse(latest.createdAtUtc)
          ? document
          : latest,
      undefined,
    );

    if (!latestDoc) return [];

    return [
      `Tóm tắt 3 luận điểm trọng tâm trong tài liệu '${latestDoc.fileName}'.`,
      `Các rủi ro hoặc khuyến nghị chính được nêu trong '${latestDoc.fileName}' là gì?`,
      `Trích xuất các mốc thời gian và số liệu quan trọng trong '${latestDoc.fileName}'.`,
    ];
  }, [documents, isShowcase]);
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
  const [pdfViewerOpen, setPdfViewerOpen] = useState(isShowcase);
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
  const [messagesLoading, setMessagesLoading] = useState(false);
  const [messagesError, setMessagesError] = useState<string | null>(null);
  const [historyRequest, setHistoryRequest] = useState(0);

  const handleConversationResolved = useCallback(
    (conversationId: string, title?: string) => {
      setLoadedConversationId(conversationId);
      selectConversation(conversationId);
      if (title) updateConversationTitle(conversationId, title);
    },
    [updateConversationTitle, selectConversation],
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
        if (controller.signal.aborted) return;
        // Post-stream history refresh must not erase a provider/quota error before it can be read.
        replaceMessages(items, true);
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
    historyRequest,
  ]);

  const indexedDocuments = useMemo(
    () => documents.filter((document) => document.status === "Indexed"),
    [documents],
  );
  const selectedDocument = useMemo(
    () =>
      documents.find((document) => document.id === selectedDocumentId) ??
      (isShowcase && selectedDocumentId === null
        ? indexedDocuments.find((document) => document.fileName === SHOWCASE_DEFAULT_DOCUMENT) ??
          indexedDocuments.find((document) => document.detectedFormat === "Pdf") ?? indexedDocuments[0] ?? null
        : null),
    [documents, indexedDocuments, isShowcase, selectedDocumentId],
  );
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
    loadedConversationId === activeConversationId || Boolean(messagesError) ||
    isStreaming;

  const selectConversationAndReset = useCallback(
    (conversationId: string) => {
      if (isStreaming) {
        return;
      }

      if (conversationId === activeConversationId && !messagesError) return;
      replaceMessages([]);
      setHistoryRequest((current) => current + 1);
      setMessagesLoading(true);
      setMessagesError(null);
      setLoadedConversationId(null);
      setSelectedCitation(null);
      setActiveCitationKey(null);
      selectConversation(conversationId);
      scrollToBottom("auto");
    },
    [activeConversationId, messagesError, isStreaming, replaceMessages, scrollToBottom, selectConversation],
  );

  const createNewConversation = useCallback(async () => {
    if (isStreaming) {
      return;
    }

    if (isShowcase) {
      selectConversation(null);
      replaceMessages([]);
      setInput("");
      setMessagesLoading(false);
      setMessagesError(null);
      setLoadedConversationId(null);
      setSelectedCitation(null);
      setActiveCitationKey(null);
      scrollToBottom("auto");
      return;
    }

    const created = await createConversation("Cuộc trò chuyện mới");
    replaceMessages([]);
    setMessagesLoading(true);
    setMessagesError(null);
    setLoadedConversationId(null);
    setSelectedCitation(null);
    setActiveCitationKey(null);
    selectConversation(created.id);
    scrollToBottom("auto");
  }, [
    createConversation,
    isShowcase,
    replaceMessages,
    isStreaming,
    scrollToBottom,
    selectConversation,
  ]);

  const removeConversation = useCallback(
    async (conversationId: string) => {
      if (isStreaming || isShowcase) {
        return;
      }

      await deleteConversation(conversationId);

      if (conversationId === activeConversationId) {
        replaceMessages([]);
        setLoadedConversationId(null);
        setMessagesLoading(true);
        setMessagesError(null);
        setSelectedCitation(null);
        setActiveCitationKey(null);
      }
    },
    [activeConversationId, deleteConversation, isShowcase, isStreaming, replaceMessages],
  );

  const submitMessage = useCallback((suggestion?: string) => {
    const message = (suggestion ?? input).trim();

    if (!message || isStreaming || conversationsLoading || messagesLoading || messagesError) {
      return;
    }

    setInput("");
    setMessagesLoading(false);
    setLoadedConversationId(activeConversationId);
    void sendMessage(message);
  }, [
    activeConversationId,
    conversationsLoading,
    messagesLoading,
    messagesError,
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
        Math.max(30, ((bounds.right - event.clientX) / bounds.width) * 100),
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
        nextWidth += 2;
      } else if (event.key === "ArrowRight") {
        nextWidth -= 2;
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
  const inputDisabled = conversationsLoading || historyUnavailable || Boolean(messagesError);
  const disabledReason = messagesError
    ? "Không thể tải lịch sử hội thoại. Hãy chọn lại hoặc tạo hội thoại mới."
    : inputDisabled ? "Đang tải dữ liệu hội thoại..." : undefined;

  return (
    <>
      <section className={cn("glass-panel flex h-[calc(100vh-11.5rem)] min-h-[640px] overflow-hidden rounded-2xl", isShowcase && pdfViewerOpen && "min-h-[960px] lg:min-h-[640px]")}>
        <ConversationSidebar
          activeConversationId={activeConversationId}
          conversations={conversations}
          disabled={isStreaming}
          error={conversationsError}
          isLoading={conversationsLoading}
          mobileOpen={mobileSidebarOpen}
          onCreate={createNewConversation}
          onDelete={removeConversation}
          canDelete={!isShowcase}
          onMobileClose={closeMobileSidebar}
          onSelect={selectConversationAndReset}
        />

        <div
          className={cn("relative flex min-h-0 min-w-0 flex-1", isShowcase && "flex-col lg:flex-row")}
          ref={splitAreaRef}
          style={
            {
              "--pdf-pane-width": `${pdfPaneWidth}%`,
            } as CSSProperties
          }
        >
          {pdfViewerOpen && selectedDocument ? (
            <aside className={cn("order-3 min-w-0 border-l border-line-subtle bg-surface lg:relative lg:z-auto lg:w-[var(--pdf-pane-width)] lg:shrink-0", isShowcase ? "relative h-72 w-full shrink-0 border-t lg:h-auto lg:border-t-0" : "absolute inset-0 z-30 w-full")}>
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
              aria-label="Thay đổi độ rộng trình xem tài liệu"
              aria-orientation="vertical"
              aria-valuemax={68}
              aria-valuemin={30}
              aria-valuenow={Math.round(pdfPaneWidth)}
              className={cn(
                "group relative z-10 order-2 hidden w-4 shrink-0 touch-none cursor-col-resize items-center justify-center bg-transparent transition-colors hover:bg-splitter-hit-active focus-visible:bg-splitter-hit-active focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-focus-ring lg:flex",
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

          {!selectedDocument && (documentsLoading || documentsError || documents.length === 0) ? (
            <aside aria-label="Trình xem tài liệu" className="order-3 hidden w-[var(--pdf-pane-width)] shrink-0 items-center justify-center border-l border-line-subtle bg-surface/35 p-8 text-center lg:flex">
              <div role="status" className="max-w-sm text-sm leading-6 text-muted">
                {documentsLoading ? <><Spinner className="mx-auto mb-3 size-5" />Đang tải tài liệu...</>
                  : documentsError ? <>Không thể tải danh sách tài liệu. {documentsError}</>
                  : <><FileText className="mx-auto mb-4 size-10 text-accent" />{isShowcase ? "Tài liệu trải nghiệm chưa sẵn sàng. Vui lòng quay lại sau." : "Chưa có tài liệu nào trong không gian làm việc này. Bạn có thể tải lên tài liệu để AI trích dẫn bằng chứng chi tiết."}</>}
              </div>
            </aside>
          ) : null}

          <div className="order-1 flex min-h-0 min-w-0 flex-1 flex-col">
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
                {activeConversation?.title ?? (isShowcase ? "Cuộc trò chuyện mới" : "Hỏi đáp tài liệu")}
              </h1>
              <p className="mt-0.5 flex items-center gap-1.5 text-xs text-muted">
                <BookOpenCheck className="size-3.5" />
                {indexedDocuments.length} tài liệu sẵn sàng
              </p>
            </div>
            <div className="hidden min-w-0 items-center gap-1.5 md:flex">
              <label className="sr-only" htmlFor="chat-document-selector">
                Chọn tài liệu
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
                value={selectedDocument?.id ?? ""}
              >
                <option value="">Chọn tài liệu</option>
                {documents.map((document) => (
                  <option key={document.id} value={document.id}>
                    {document.title || document.fileName}
                  </option>
                ))}
              </select>
            </div>
            <Button
              aria-label={pdfViewerOpen ? "Đóng trình xem tài liệu" : "Mở trình xem tài liệu"}
              className="size-9 shrink-0 px-0"
              disabled={documentsLoading || documents.length === 0}
              onClick={() => {
                if (pdfViewerOpen) {
                  closePdfViewer();
                  return;
                }

                const documentId = selectedDocument?.id ?? documents[0]?.id;
                if (documentId) {
                  openDocument(documentId, pdfTarget?.pageNumber ?? 1);
                }
              }}
              title={pdfViewerOpen ? "Đóng trình xem tài liệu" : "Mở trình xem tài liệu"}
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

          {isShowcase ? <ShowcaseBanner /> : null}

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
                  disabled={inputDisabled || (isShowcase && indexedDocuments.length === 0)}
                  onSuggestion={isShowcase ? submitMessage : setInput}
                  prompts={isShowcase || (!documentsLoading && !documentsError) ? suggestedPrompts : []}
                  showOnboarding={!isShowcase && !documentsLoading && !documentsError && documents.length === 0}
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
  prompts,
  showOnboarding,
}: {
  workspaceName: string;
  disabled: boolean;
  onSuggestion: (prompt: string) => void;
  prompts: string[];
  showOnboarding: boolean;
}) {
  return (
    <div className="flex min-h-full items-center justify-center px-5 py-10">
      <div className="w-full max-w-2xl text-center">
        <span className="glow-border mx-auto flex size-20 items-center justify-center rounded-full p-1 shadow-[0_0_32px_var(--brand-icon-shadow)]">
          <Image
            alt="Biểu tượng OmniDoc"
            className="size-[72px] rounded-full"
            height={72}
            src="/images/logo-icon.png"
            width={72}
          />
        </span>
        <h2 className="mt-6 text-2xl font-semibold tracking-tight text-content">
          <BrandName /> RAG Assistant
        </h2>
        <p className="mt-1.5 text-sm font-medium text-accent">
          Intelligence in every document
        </p>
        <p className="mx-auto mt-3 max-w-lg text-sm leading-6 text-muted">
          Khám phá tri thức trong {workspaceName}.{" "}
          Đặt câu hỏi để nhận câu trả lời có căn cứ, kèm trích dẫn đến đúng tài
          liệu và số trang.
        </p>

        {showOnboarding ? (
          <div className="mt-7 grid gap-2.5 text-left sm:grid-cols-2">
            {ONBOARDING_HINTS.map((hint) => (
              <p className="glass-panel rounded-2xl p-3.5 text-xs leading-5 text-content-secondary" key={hint}>
                <Sparkles className="mb-2 size-4 text-accent" />
                {hint}
              </p>
            ))}
          </div>
        ) : null}
        <div className="mt-7 grid gap-2.5 text-left sm:grid-cols-3">
          {prompts.map((prompt) => (
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
