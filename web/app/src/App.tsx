import { useState, type ChangeEvent, type DragEvent } from "react";
import { coreMigrationStatus } from "@fireurq/core";
import {
  loadQuestFromFile,
  loadQuestFromRequest,
  playerMigrationStatus,
  resolveQuestSource,
  type LoadedQuestDocument
} from "@fireurq/player";
import { appConfig } from "./app-config";

export function App() {
  const [sourceInput, setSourceInput] = useState("");
  const [loading, setLoading] = useState(false);
  const [loadedQuest, setLoadedQuest] = useState<LoadedQuestDocument | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleLoadFromInput() {
    if (!sourceInput.trim()) {
      setError("Enter a quest URL or a filename first.");
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const document = await loadQuestFromRequest(sourceInput, {
        defaultBaseUrl: appConfig.defaultQuestLibraryBaseUrl
      });
      setLoadedQuest(document);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Failed to load quest.");
    } finally {
      setLoading(false);
    }
  }

  async function handleFile(file: File | null) {
    if (!file) {
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const document = await loadQuestFromFile(file);
      setLoadedQuest(document);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Failed to read quest file.");
    } finally {
      setLoading(false);
    }
  }

  function handleFileInput(event: ChangeEvent<HTMLInputElement>) {
    void handleFile(event.target.files?.[0] ?? null);
    event.target.value = "";
  }

  function handleDrop(event: DragEvent<HTMLLabelElement>) {
    event.preventDefault();
    void handleFile(event.dataTransfer.files?.[0] ?? null);
  }

  const resolvedSource = sourceInput.trim()
    ? safeResolve(sourceInput, appConfig.defaultQuestLibraryBaseUrl)
    : null;

  return (
    <main className="shell">
      <section className="hero">
        <p className="eyebrow">FireURQ Web</p>
        <h1>Quest Source Gateway</h1>
        <p className="lede">
          The player now prefers quest loading by source string. Absolute URLs
          load as-is, plain filenames resolve through the default quest library,
          and empty input falls back to a local file dropzone.
        </p>
      </section>

      <section className="loader">
        <div className="loader-card">
          <h2>Load Quest</h2>
          <p className="hint">
            Examples: `https://someweb.com/quests/hamster.qst` or `hamster.qst`
          </p>
          <div className="source-row">
            <input
              className="source-input"
              value={sourceInput}
              onChange={(event) => setSourceInput(event.target.value)}
              placeholder="Quest URL or filename"
            />
            <button className="load-button" onClick={() => void handleLoadFromInput()} disabled={loading}>
              {loading ? "Loading..." : "Load"}
            </button>
          </div>
          <p className="hint">
            Default library base: <span className="mono">{appConfig.defaultQuestLibraryBaseUrl}</span>
          </p>
          {resolvedSource ? (
            <div className="resolution">
              <span className="resolution-label">Resolved</span>
              <span className="mono">{resolvedSource.url}</span>
            </div>
          ) : (
            <label
              className="dropzone"
              onDragOver={(event) => event.preventDefault()}
              onDrop={handleDrop}
            >
              <input type="file" accept=".qst,.txt" onChange={handleFileInput} />
              <span>Drop a local quest here or choose a file from the device</span>
            </label>
          )}
          {error ? <p className="error">{error}</p> : null}
        </div>

        <div className="loader-card">
          <h2>Loaded Quest</h2>
          {loadedQuest ? (
            <div className="meta-grid">
              <Meta label="Source" value={describeSource(loadedQuest)} />
              <Meta label="Encoding" value={loadedQuest.encodingName} />
              <Meta label="Confidence" value={loadedQuest.confidence.toFixed(2)} />
              <Meta label="BOM" value={loadedQuest.bomDetected ? "yes" : "no"} />
              <Meta label="Lines" value={String(loadedQuest.lineCount)} />
              <Meta label="Parse Diags" value={String(loadedQuest.parseDiagnosticsCount)} />
              <Meta label="Compiler Diags" value={String(loadedQuest.compilerDiagnosticsCount)} />
              <Meta label="Preview" value={firstPreviewLine(loadedQuest.text)} />
            </div>
          ) : (
            <p className="hint">No quest loaded yet.</p>
          )}
        </div>
      </section>

      <section className="grid">
        <article className="card">
          <h2>Core</h2>
          <p>{coreMigrationStatus.summary}</p>
          <ul>
            {coreMigrationStatus.nextSteps.map((step) => (
              <li key={step}>{step}</li>
            ))}
          </ul>
        </article>

        <article className="card">
          <h2>Player</h2>
          <p>{playerMigrationStatus.summary}</p>
          <ul>
            {playerMigrationStatus.nextSteps.map((step) => (
              <li key={step}>{step}</li>
            ))}
          </ul>
        </article>
      </section>
    </main>
  );
}

function Meta(props: { label: string; value: string }) {
  return (
    <div className="meta-card">
      <span className="meta-label">{props.label}</span>
      <span className="meta-value">{props.value}</span>
    </div>
  );
}

function describeSource(document: LoadedQuestDocument): string {
  if (document.source.kind === "file") {
    return document.source.name;
  }

  return document.source.url;
}

function firstPreviewLine(text: string): string {
  return text.split(/\r?\n/u).find((line) => line.trim().length > 0)?.slice(0, 120) ?? "(empty)";
}

function safeResolve(request: string, defaultBaseUrl: string) {
  try {
    return resolveQuestSource(request, { defaultBaseUrl });
  } catch {
    return null;
  }
}
