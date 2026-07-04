import { useEffect, useState } from "react";
import { api, Config, Session } from "./api";
import SetupForm from "./components/SetupForm";
import SessionView from "./components/SessionView";

export default function App() {
  const [config, setConfig] = useState<Config | null>(null);
  const [configError, setConfigError] = useState<string | null>(null);
  const [session, setSession] = useState<Session | null>(null);

  useEffect(() => {
    api.getConfig().then(setConfig).catch((e: Error) => setConfigError(e.message));
  }, []);

  return (
    <div className="app">
      <header className="app-header">
        <h1>
          Code <span className="accent">Modernizer</span>
        </h1>
        {session && (
          <button className="btn subtle" onClick={() => setSession(null)}>
            New session
          </button>
        )}
      </header>

      {configError && <div className="banner error">Failed to load config: {configError}</div>}

      {!config && !configError && <div className="banner">Loading…</div>}

      {config && !session && <SetupForm config={config} onStarted={setSession} />}

      {config && session && (
        <SessionView initialSession={session} onSessionUpdate={setSession} />
      )}
    </div>
  );
}
