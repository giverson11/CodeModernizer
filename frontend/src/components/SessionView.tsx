import { useCallback, useEffect, useRef, useState } from "react";
import { api, FileDetail, FileSummary, Session } from "../api";
import DiffViewer from "./DiffViewer";
import LogTerminal from "./LogTerminal";

interface Props {
  initialSession: Session;
  onSessionUpdate: (session: Session) => void;
}

const ACTIVE_FILE_STATUSES = new Set(["Pending", "Modernizing"]);

// Plain-language wording for the overview model's raw verdict codes.
const VERDICT_INFO: Record<string, { label: string; explanation: string }> = {
  EQUIVALENT: {
    label: "✓ Behaves the same",
    explanation:
      "The reviewer compared the modernized code against the original and found no behavior changes — it should produce the same output.",
  },
  POTENTIALLY_DIFFERENT: {
    label: "⚠ Behavior may have changed",
    explanation:
      "The reviewer found changes that could make the modernized code behave differently from the original. Read its concerns below before applying.",
  },
  INSUFFICIENT_INFO: {
    label: "? Couldn't verify",
    explanation:
      "The reviewer didn't have enough context to tell whether the behavior changed. Verify the changes manually before applying.",
  },
};

function verdictInfo(verdict: string) {
  return (
    VERDICT_INFO[verdict] ?? {
      label: verdict.replace(/_/g, " "),
      explanation: "",
    }
  );
}

function statusIcon(file: FileSummary): string {
  switch (file.status) {
    case "Pending": return "○";
    case "Modernizing": return "◐";
    case "Ready": return file.applied ? "✔" : "●";
    case "Unchanged": return "—";
    case "Failed": return "✕";
  }
}

export default function SessionView({ initialSession, onSessionUpdate }: Props) {
  const [session, setSession] = useState(initialSession);
  const [selectedFileId, setSelectedFileId] = useState<string | null>(null);
  const [detail, setDetail] = useState<FileDetail | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [applied, setApplied] = useState<string[] | null>(null);
  const [showReviewTerminal, setShowReviewTerminal] = useState(false);
  const lastFileStatuses = useRef<Map<string, string>>(new Map());
  const prevStatus = useRef(initialSession.status);

  const isBusy =
    session.status === "Running" ||
    session.status === "Scanning" ||
    session.status === "Reviewing" ||
    session.files.some((f) => ACTIVE_FILE_STATUSES.has(f.status));

  const refreshSession = useCallback(async () => {
    try {
      const updated = await api.getSession(session.id);
      setSession(updated);
      onSessionUpdate(updated);
      return updated;
    } catch {
      return null;
    }
  }, [session.id, onSessionUpdate]);

  // Poll while the modernizer, an adjustment, or the review is running.
  useEffect(() => {
    if (!isBusy) return;
    const timer = setInterval(refreshSession, 2000);
    return () => clearInterval(timer);
  }, [isBusy, refreshSession]);

  // Re-fetch the open file when its status changes (e.g. adjust finished).
  useEffect(() => {
    for (const file of session.files) {
      const previous = lastFileStatuses.current.get(file.id);
      if (file.id === selectedFileId && previous && previous !== file.status) {
        api.getFile(session.id, file.id).then(setDetail).catch(() => undefined);
      }
      lastFileStatuses.current.set(file.id, file.status);
    }
  }, [session, selectedFileId]);

  const openFile = async (fileId: string) => {
    setSelectedFileId(fileId);
    setDetail(null);
    try {
      setDetail(await api.getFile(session.id, fileId));
    } catch (e) {
      setActionError((e as Error).message);
    }
  };

  const runAction = async (action: () => Promise<FileDetail | void>) => {
    setActionError(null);
    try {
      const result = await action();
      if (result) setDetail(result);
      await refreshSession();
    } catch (e) {
      setActionError((e as Error).message);
    }
  };

  const startReview = () => {
    setShowReviewTerminal(true);
    return runAction(() => api.startReview(session.id));
  };

  // Pop the review panel open when a review starts (but let the user close it
  // mid-review without it snapping back open on the next poll).
  useEffect(() => {
    if (session.status === "Reviewing" && prevStatus.current !== "Reviewing") {
      setShowReviewTerminal(true);
    }
    prevStatus.current = session.status;
  }, [session.status]);

  const implementReview = () => {
    setShowReviewTerminal(false);
    return runAction(async () => {
      await api.implementReview(session.id);
    });
  };

  const selectedSummary = session.files.find((f) => f.id === selectedFileId);

  const fetchReviewLog = useCallback(
    (from: number) => api.getReviewLog(session.id, from),
    [session.id],
  );

  const applyChanges = async () => {
    const acceptedFiles = session.files.filter((f) => f.acceptedCount > 0).length;
    if (!window.confirm(`Write accepted changes for ${acceptedFiles} file(s) to disk?`)) return;
    setActionError(null);
    try {
      const result = await api.apply(session.id);
      setApplied(result.written);
      await refreshSession();
    } catch (e) {
      setActionError((e as Error).message);
    }
  };

  const done = session.files.filter((f) => !ACTIVE_FILE_STATUSES.has(f.status)).length;
  const anyAccepted = session.files.some((f) => f.acceptedCount > 0);

  return (
    <div className="session">
      <aside className="sidebar">
        <div className="progress-box">
          <div className="progress-label">
            {session.status === "Running" || session.status === "Scanning"
              ? `Modernizing ${done}/${session.files.length} files…`
              : `${session.files.length} files scanned`}
          </div>
          <div className="progress-bar">
            <div
              className="progress-fill"
              style={{ width: `${session.files.length ? (done / session.files.length) * 100 : 0}%` }}
            />
          </div>
          <div className="progress-meta">
            {session.projectPath}
            <br />
            agent: {session.agentModelId} · review: {session.reviewModelId}
          </div>
        </div>

        <ul className="file-list">
          {session.files.map((file) => (
            <li key={file.id}>
              <button
                className={`file-item ${file.id === selectedFileId ? "selected" : ""} status-${file.status.toLowerCase()}`}
                onClick={() => openFile(file.id)}
                disabled={file.status === "Pending"}
              >
                <span className="file-icon">{statusIcon(file)}</span>
                <span className="file-path">{file.relativePath}</span>
                {file.hunkCount > 0 && (
                  <span className="file-counts">
                    {file.acceptedCount + file.rejectedCount}/{file.hunkCount}
                  </span>
                )}
              </button>
            </li>
          ))}
          {session.files.length === 0 && <li className="empty">No matching files found.</li>}
        </ul>

        <div className="sidebar-actions">
          <button
            className="btn"
            onClick={() =>
              session.status === "Reviewing" ? setShowReviewTerminal(true) : startReview()
            }
            disabled={session.status !== "Completed" && session.status !== "Reviewing"}
            title="Ask the overview model whether the program still behaves the same"
          >
            {session.status === "Reviewing" ? "Reviewing… (show log)" : "Run overview check"}
          </button>
          <button className="btn primary" onClick={applyChanges} disabled={!anyAccepted || isBusy}>
            Apply accepted changes
          </button>
        </div>

        {session.review && (
          <button
            className={`verdict-pill verdict-${session.review.verdict.toLowerCase()}`}
            onClick={() => setShowReviewTerminal(true)}
            title={`${verdictInfo(session.review.verdict).explanation} Click to see the full review.`}
          >
            {verdictInfo(session.review.verdict).label}
          </button>
        )}

        {applied && (
          <div className="banner success">
            Wrote {applied.length} file(s) to disk.
          </div>
        )}
        {session.error && <div className="banner error">{session.error}</div>}
        {actionError && <div className="banner error">{actionError}</div>}
      </aside>

      <main className="diff-pane">
        {!detail && !selectedFileId && (
          <div className="placeholder">Select a file to review its changes.</div>
        )}
        {!detail && selectedFileId && selectedSummary?.status !== "Modernizing" && (
          <div className="placeholder">Loading file…</div>
        )}
        {selectedSummary?.status === "Modernizing" && (
          <div className="placeholder">The agent is working on this file…</div>
        )}
        {detail && selectedSummary?.status !== "Modernizing" && (
          <DiffViewer
            key={`diff-${detail.id}`}
            sessionId={session.id}
            detail={detail}
            onDetailChange={setDetail}
            onSummaryChange={refreshSession}
          />
        )}
      </main>

      {showReviewTerminal && (
        <div className="review-overlay" onClick={() => setShowReviewTerminal(false)}>
          <div className="review-panel" onClick={(e) => e.stopPropagation()}>
            <LogTerminal
              title={`Overview model — ${session.reviewModelId}`}
              active={session.status === "Reviewing"}
              fetchChunk={fetchReviewLog}
              onClose={() => setShowReviewTerminal(false)}
            />
            {session.review && (
              <div className="review-result">
                <div className={`review-verdict verdict-${session.review.verdict.toLowerCase()}`}>
                  {verdictInfo(session.review.verdict).label}
                </div>
                <p className="review-explanation">
                  {verdictInfo(session.review.verdict).explanation}
                </p>
                <p className="review-summary">{session.review.summary}</p>
                <div className="review-result-actions">
                  <button className="btn subtle" onClick={() => setShowReviewTerminal(false)}>
                    Close
                  </button>
                  {session.review.verdict === "POTENTIALLY_DIFFERENT" && (
                    <button
                      className="btn primary"
                      onClick={implementReview}
                      title="Send the reviewer's concerns back to the agent model to fix"
                    >
                      Implement requested changes
                    </button>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
