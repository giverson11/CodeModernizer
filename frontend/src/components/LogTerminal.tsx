import { useEffect, useRef, useState } from "react";
import { LogChunk } from "../api";

interface Props {
  title: string;
  /** True while the model is still streaming; controls polling and the status dot. */
  active: boolean;
  /** Incremental fetch: returns the log tail written after `from`. */
  fetchChunk: (from: number) => Promise<LogChunk>;
  onClose?: () => void;
}

const POLL_MS = 700;

export default function LogTerminal({ title, active, fetchChunk, onClose }: Props) {
  const [text, setText] = useState("");
  const [collapsed, setCollapsed] = useState(false);
  const offsetRef = useRef(0);
  const bodyRef = useRef<HTMLPreElement>(null);
  const pinnedToBottom = useRef(true);

  useEffect(() => {
    let cancelled = false;
    let timer: number | undefined;

    const poll = async () => {
      try {
        const { next, chunk } = await fetchChunk(offsetRef.current);
        if (cancelled) return;
        if (next < offsetRef.current) {
          // Log was reset server-side (e.g. a new adjust run): start over.
          offsetRef.current = 0;
          setText("");
          return;
        }
        if (chunk) {
          offsetRef.current = next;
          setText((prev) => prev + chunk);
        }
      } catch {
        /* transient poll failure; retry on next tick */
      }
    };

    void poll();
    if (active) timer = window.setInterval(() => void poll(), POLL_MS);
    return () => {
      cancelled = true;
      if (timer) window.clearInterval(timer);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [active, fetchChunk]);

  // Autoscroll while the user hasn't scrolled up to read something.
  useEffect(() => {
    const el = bodyRef.current;
    if (el && pinnedToBottom.current) el.scrollTop = el.scrollHeight;
  }, [text, collapsed]);

  if (!text && !active) return null;

  return (
    <div className="terminal">
      <div className="terminal-header" onClick={() => setCollapsed((c) => !c)}>
        <span className={`terminal-dot ${active ? "live" : "done"}`} />
        <span className="terminal-title">{title}</span>
        <span className="terminal-spacer" />
        <button
          className="terminal-btn"
          onClick={(e) => {
            e.stopPropagation();
            setCollapsed((c) => !c);
          }}
          title={collapsed ? "Expand" : "Collapse"}
        >
          {collapsed ? "▲" : "▼"}
        </button>
        {onClose && (
          <button
            className="terminal-btn"
            onClick={(e) => {
              e.stopPropagation();
              onClose();
            }}
            title="Close"
          >
            ✕
          </button>
        )}
      </div>
      {!collapsed && (
        <pre
          ref={bodyRef}
          className="terminal-body"
          onScroll={(e) => {
            const el = e.currentTarget;
            pinnedToBottom.current = el.scrollHeight - el.scrollTop - el.clientHeight < 30;
          }}
        >
          {text}
          {active && <span className="terminal-cursor">▋</span>}
        </pre>
      )}
    </div>
  );
}
