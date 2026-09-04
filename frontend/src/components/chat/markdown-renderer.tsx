"use client";

import { Check, Copy } from "lucide-react";
import {
  isValidElement,
  memo,
  useEffect,
  useDeferredValue,
  useRef,
  useState,
  type ComponentPropsWithoutRef,
  type ReactNode,
} from "react";
import ReactMarkdown, { type Components } from "react-markdown";
import rehypeHighlight from "rehype-highlight";
import remarkGfm from "remark-gfm";

function extractText(node: ReactNode): string {
  if (typeof node === "string" || typeof node === "number") {
    return String(node);
  }

  if (Array.isArray(node)) {
    return node.map(extractText).join("");
  }

  if (isValidElement<{ children?: ReactNode }>(node)) {
    return extractText(node.props.children);
  }

  return "";
}

function CodeBlock({
  children,
  ...props
}: ComponentPropsWithoutRef<"pre">) {
  const [copied, setCopied] = useState(false);
  const resetTimerRef = useRef<number | null>(null);
  const code = extractText(children).replace(/\n$/, "");

  useEffect(
    () => () => {
      if (resetTimerRef.current !== null) {
        window.clearTimeout(resetTimerRef.current);
      }
    },
    [],
  );

  async function copyCode() {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(true);
      if (resetTimerRef.current !== null) {
        window.clearTimeout(resetTimerRef.current);
      }
      resetTimerRef.current = window.setTimeout(() => {
        resetTimerRef.current = null;
        setCopied(false);
      }, 1_500);
    } catch {
      setCopied(false);
    }
  }

  return (
    <div className="markdown-code-block group relative">
      <div className="absolute inset-x-0 top-0 z-10 flex h-10 items-center justify-between border-b border-line bg-code-control px-3 text-code-control-content">
        <span className="text-[11px] font-medium uppercase tracking-wider">
          Mã nguồn
        </span>
        <button
          aria-label="Sao chép mã"
          className="inline-flex h-8 items-center gap-1.5 rounded-lg px-2 text-xs transition-colors hover:bg-code-control-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
          onClick={() => void copyCode()}
          type="button"
        >
          {copied ? (
            <Check className="size-3.5 text-success" />
          ) : (
            <Copy className="size-3.5" />
          )}
          {copied ? "Đã chép" : "Sao chép"}
        </button>
      </div>
      <pre {...props}>{children}</pre>
    </div>
  );
}

const markdownComponents: Components = {
  pre: CodeBlock,
  a: ({ children, ...props }) => (
    <a {...props} rel="noreferrer" target="_blank">
      {children}
    </a>
  ),
  table: ({ children, ...props }) => (
    <div className="markdown-table-wrapper">
      <table {...props}>{children}</table>
    </div>
  ),
  code: ({ className, children, ...props }) => {
    const isBlock = Boolean(className) || String(children).includes("\n");

    if (isBlock) {
      return (
        <code className={className} {...props}>
          {children}
        </code>
      );
    }

    return (
      <code className="markdown-inline-code" {...props}>
        {children}
      </code>
    );
  },
};

function MarkdownRendererComponent({ content }: { content: string }) {
  const deferredContent = useDeferredValue(content);

  return (
    <div className="markdown-body">
      <ReactMarkdown
        components={markdownComponents}
        rehypePlugins={[
          [rehypeHighlight, { detect: true, ignoreMissing: true }],
        ]}
        remarkPlugins={[remarkGfm]}
        skipHtml
      >
        {deferredContent}
      </ReactMarkdown>
    </div>
  );
}

export const MarkdownRenderer = memo(MarkdownRendererComponent);
