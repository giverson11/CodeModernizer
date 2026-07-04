import { useMemo, useState } from "react";
import { api, FileDetail, Hunk, HunkDecision } from "../api";
import AdjustDialog from "./AdjustDialog";

interface Props {
  sessionId: string;
  detail: FileDetail;
  onDetailChange: (detail: FileDetail) => void;
  onSummaryChange: () => void;
}

type Row =
  | { kind: "context"; text: string; leftNo: number; rightNo: number }
  | { kind: "hunk"; hunk: Hunk };

/**
 * Interleaves unchanged context lines with change hunks so the viewer reads
 * like a merge-conflict tool: context spans both columns, hunks split into
 * original (left) and modernized (right).
 */
function buildRows(detail: FileDetail): Row[] {
  const originalLines = detail.originalContent.replace(/\r\n/g, "\n").split("\n");
  const hunks = [...detail.hunks].sort((a, b) => a.originalStart - b.originalStart);
  const rows: Row[] = [];
  let cursor = 0;
  let rightNo = 1;

  for (const hunk of hunks) {
    for (; cursor < hunk.originalStart && cursor < originalLines.length; cursor++) {
      rows.push({ kind: "context", text: originalLines[cursor], leftNo: cursor + 1, rightNo });
      rightNo++;
    }
    rows.push({ kind: "hunk", hunk });
    cursor += hunk.originalLines.length;
    rightNo += hunk.decision === "Rejected" ? hunk.originalLines.length : hunk.modernizedLines.length;
  }

  for (; cursor < originalLines.length; cursor++) {
    rows.push({ kind: "context", text: originalLines[cursor], leftNo: cursor + 1, rightNo });
    rightNo++;
  }

  return rows;
}

export default function DiffViewer({ sessionId, detail, onDetailChange, onSummaryChange }: Props) {
  const [adjustOpen, setAdjustOpen] = useState(false);
  const [adjusting, setAdjusting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const rows = useMemo(() => buildRows(detail), [detail]);

  const withRefresh = async (action: () => Promise<FileDetail>) => {
    setError(null);
    try {
      onDetailChange(await action());
      onSummaryChange();
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const setDecision = (hunkId: number, decision: HunkDecision) =>
    withRefresh(() => api.setHunkDecision(sessionId, detail.id, hunkId, decision));

  const submitAdjust = async (instructions: string) => {
    setAdjustOpen(false);
    setAdjusting(true);
    setError(null);
    try {
      await api.adjustFile(sessionId, detail.id, instructions);
      onSummaryChange(); // polling picks up Modernizing -> Ready and refetches
    } catch (e) {
      setError((e as Error).message);
      setAdjusting(false);
    }
  };

  const isModernizing = detail.status === "Modernizing" || adjusting;

  return (
    <div className="diff-viewer">
      <div className="diff-toolbar">
        <span className="diff-title">{detail.relativePath}</span>
        <div className="diff-actions">
          <button className="btn accept" onClick={() => withRefresh(() => api.acceptFile(sessionId, detail.id))} disabled={isModernizing}>
            Accept all
          </button>
          <button className="btn reject" onClick={() => withRefresh(() => api.revertFile(sessionId, detail.id))} disabled={isModernizing}>
            Revert all
          </button>
          <button className="btn" onClick={() => setAdjustOpen(true)} disabled={isModernizing}>
            {isModernizing ? "Adjusting…" : "Request AI adjustments"}
          </button>
        </div>
      </div>

      {error && <div className="banner error">{error}</div>}
      {detail.status === "Failed" && detail.error && (
        <div className="banner error">Modernization failed: {detail.error}</div>
      )}
      {isModernizing && <div className="banner">The agent is revising this file…</div>}

      <div className="diff-columns-header">
        <div>Original</div>
        <div>Modernized</div>
      </div>

      <div className="diff-body">
        {rows.map((row, index) =>
          row.kind === "context" ? (
            <div className="diff-row context" key={`c${index}`}>
              <span className="lineno">{row.leftNo}</span>
              <pre className="code">{row.text}</pre>
              <span className="lineno">{row.rightNo}</span>
              <pre className="code">{row.text}</pre>
            </div>
          ) : (
            <HunkBlock
              key={`h${row.hunk.id}`}
              hunk={row.hunk}
              disabled={isModernizing}
              onDecision={setDecision}
            />
          ),
        )}
      </div>

      {adjustOpen && (
        <AdjustDialog onSubmit={submitAdjust} onCancel={() => setAdjustOpen(false)} />
      )}
    </div>
  );
}

function HunkBlock({
  hunk,
  disabled,
  onDecision,
}: {
  hunk: Hunk;
  disabled: boolean;
  onDecision: (hunkId: number, decision: HunkDecision) => void;
}) {
  const height = Math.max(hunk.originalLines.length, hunk.modernizedLines.length);
  const rows = Array.from({ length: height }, (_, i) => ({
    left: hunk.originalLines[i],
    right: hunk.modernizedLines[i],
  }));

  return (
    <div className={`hunk decision-${hunk.decision.toLowerCase()}`}>
      <div className="hunk-header">
        <span className="hunk-label">
          Change @ line {hunk.originalStart + 1}
          {hunk.decision !== "Pending" && <em> · {hunk.decision}</em>}
        </span>
        <div className="hunk-buttons">
          <button
            className={`btn tiny accept ${hunk.decision === "Accepted" ? "active" : ""}`}
            onClick={() => onDecision(hunk.id, "Accepted")}
            disabled={disabled}
          >
            Accept
          </button>
          <button
            className={`btn tiny reject ${hunk.decision === "Rejected" ? "active" : ""}`}
            onClick={() => onDecision(hunk.id, "Rejected")}
            disabled={disabled}
          >
            Keep original
          </button>
          {hunk.decision !== "Pending" && (
            <button className="btn tiny" onClick={() => onDecision(hunk.id, "Pending")} disabled={disabled}>
              Reset
            </button>
          )}
        </div>
      </div>
      {rows.map((row, i) => (
        <div className="diff-row" key={i}>
          <span className="lineno" />
          <pre className={`code ${row.left !== undefined ? "removed" : "blank"}`}>
            {row.left ?? ""}
          </pre>
          <span className="lineno" />
          <pre className={`code ${row.right !== undefined ? "added" : "blank"}`}>
            {row.right ?? ""}
          </pre>
        </div>
      ))}
    </div>
  );
}
