import { useState } from "react";

interface Props {
  onSubmit: (instructions: string) => void;
  onCancel: () => void;
}

export default function AdjustDialog({ onSubmit, onCancel }: Props) {
  const [instructions, setInstructions] = useState("");

  return (
    <div className="modal-backdrop" onClick={onCancel}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <h3>Request AI adjustments</h3>
        <p className="modal-hint">
          Describe what the agent should change about the modernized version of this file.
        </p>
        <textarea
          autoFocus
          rows={6}
          value={instructions}
          onChange={(e) => setInstructions(e.target.value)}
          placeholder="e.g. Keep the getX() accessors instead of converting this class to a record."
        />
        <div className="modal-actions">
          <button className="btn subtle" onClick={onCancel}>
            Cancel
          </button>
          <button
            className="btn primary"
            disabled={!instructions.trim()}
            onClick={() => onSubmit(instructions.trim())}
          >
            Send to agent
          </button>
        </div>
      </div>
    </div>
  );
}
