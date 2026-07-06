import { useEffect, useState } from "react";
import { api, BrowseResult } from "../api";

interface Props {
  initialPath?: string;
  onSelect: (path: string) => void;
  onCancel: () => void;
}

export default function DirectoryPicker({ initialPath, onSelect, onCancel }: Props) {
  const [result, setResult] = useState<BrowseResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [editPath, setEditPath] = useState("");

  const load = async (path?: string) => {
    setLoading(true);
    setError(null);
    try {
      const browsed = await api.browse(path);
      setResult(browsed);
      setEditPath(browsed.path);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    // If the typed path is invalid, fall back to the home directory.
    void (async () => {
      if (initialPath) {
        try {
          const browsed = await api.browse(initialPath);
          setResult(browsed);
          setEditPath(browsed.path);
          setLoading(false);
          return;
        } catch {
          /* fall through to home */
        }
      }
      await load();
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="modal-backdrop" onClick={onCancel}>
      <div className="modal picker" onClick={(e) => e.stopPropagation()}>
        <h3>Select project folder</h3>

        <div className="picker-path-row">
          <button
            className="btn tiny"
            disabled={loading || !result?.parent}
            onClick={() => result?.parent && load(result.parent)}
            title="Up one level"
          >
            ↑ Up
          </button>
          <input
            className="picker-path"
            type="text"
            value={editPath}
            onChange={(e) => setEditPath(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault();
                void load(editPath.trim() || undefined);
              }
            }}
            placeholder="Type or paste a path, then press Enter"
            spellCheck={false}
          />
        </div>

        {error && <div className="banner error">{error}</div>}

        <ul className="picker-list">
          {loading && <li className="picker-note">Loading…</li>}
          {!loading && result?.directories.length === 0 && (
            <li className="picker-note">No subfolders</li>
          )}
          {!loading &&
            result?.directories.map((dir) => (
              <li key={dir.path}>
                <button className="picker-dir" onClick={() => load(dir.path)}>
                  ▸ {dir.name}
                </button>
              </li>
            ))}
        </ul>

        <div className="modal-actions">
          <button className="btn subtle" onClick={onCancel}>
            Cancel
          </button>
          <button
            className="btn primary"
            disabled={!result}
            onClick={() => result && onSelect(result.path)}
          >
            Select this folder
          </button>
        </div>
      </div>
    </div>
  );
}
