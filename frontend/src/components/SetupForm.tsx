import { FormEvent, useMemo, useState } from "react";
import { api, Config, Session } from "../api";
import DirectoryPicker from "./DirectoryPicker";

interface Props {
  config: Config;
  onStarted: (session: Session) => void;
}

export default function SetupForm({ config, onStarted }: Props) {
  const [projectPath, setProjectPath] = useState("");
  const [skillId, setSkillId] = useState(config.skills[0]?.id ?? "");
  const [providerId, setProviderId] = useState(config.providers[0]?.id ?? "");
  const provider = useMemo(
    () => config.providers.find((p) => p.id === providerId),
    [config, providerId],
  );
  const [agentModelId, setAgentModelId] = useState(provider?.models[0]?.id ?? "");
  const [reviewModelId, setReviewModelId] = useState(provider?.models[0]?.id ?? "");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showPicker, setShowPicker] = useState(false);

  const selectProvider = (id: string) => {
    setProviderId(id);
    const models = config.providers.find((p) => p.id === id)?.models ?? [];
    setAgentModelId(models[0]?.id ?? "");
    setReviewModelId(models[0]?.id ?? "");
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const session = await api.startSession({
        projectPath: projectPath.trim(),
        skillId,
        providerId,
        agentModelId,
        reviewModelId,
      });
      onStarted(session);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form className="setup-card" onSubmit={submit}>
      <h2>Start a modernization run</h2>

      {!config.hasApiKey && (
        <div className="banner error">
          No Anthropic API key configured. Set <code>Anthropic:ApiKey</code> in{" "}
          <code>src/CodeModernizer.Api/appsettings.Local.json</code> (or export{" "}
          <code>ANTHROPIC_API_KEY</code>) and restart the API.
        </div>
      )}

      <label>
        Project folder
        <div className="path-row">
          <input
            type="text"
            className="path-input"
            value={projectPath}
            readOnly
            onClick={() => setShowPicker(true)}
            placeholder="Click to select a project folder…"
            required
          />
          <button type="button" className="btn" onClick={() => setShowPicker(true)}>
            Browse…
          </button>
        </div>
      </label>

      <label>
        Modernization skill
        <select value={skillId} onChange={(e) => setSkillId(e.target.value)}>
          {config.skills.map((s) => (
            <option key={s.id} value={s.id}>
              {s.displayName}
            </option>
          ))}
        </select>
      </label>

      <label>
        AI provider
        <select value={providerId} onChange={(e) => selectProvider(e.target.value)}>
          {config.providers.map((p) => (
            <option key={p.id} value={p.id}>
              {p.displayName}
            </option>
          ))}
        </select>
      </label>

      <div className="model-row">
        <label>
          Agent model (per-file modernizer)
          <select value={agentModelId} onChange={(e) => setAgentModelId(e.target.value)}>
            {provider?.models.map((m) => (
              <option key={m.id} value={m.id}>
                {m.displayName}
              </option>
            ))}
          </select>
        </label>

        <label>
          Overview model (equivalence review)
          <select value={reviewModelId} onChange={(e) => setReviewModelId(e.target.value)}>
            {provider?.models.map((m) => (
              <option key={m.id} value={m.id}>
                {m.displayName}
              </option>
            ))}
          </select>
        </label>
      </div>

      {error && <div className="banner error">{error}</div>}

      <button className="btn primary" type="submit" disabled={submitting || !projectPath.trim()}>
        {submitting ? "Starting…" : "Run modernizer"}
      </button>

      {showPicker && (
        <DirectoryPicker
          initialPath={projectPath.trim() || undefined}
          onSelect={(path) => {
            setProjectPath(path);
            setShowPicker(false);
          }}
          onCancel={() => setShowPicker(false)}
        />
      )}
    </form>
  );
}
