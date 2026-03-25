import { lazy, Suspense, useEffect, useState, type ChangeEvent, type DragEvent } from "react";
import {
  loadQuestFromFile,
  loadQuestFromRequest,
  resolveQuestSource,
  type LoadedQuestDocument
} from "@fireurq/player";
import { appConfig } from "./app-config";
import { PlayerErrorBoundary } from "./PlayerErrorBoundary";

const GraphicalPlayer = lazy(async () => import("./GraphicalPlayer").then((module) => ({
  default: module.GraphicalPlayer
})));

export function App() {
  const [loading, setLoading] = useState(false);
  const [loadedQuest, setLoadedQuest] = useState<LoadedQuestDocument | null>(null);
  const [error, setError] = useState<string | null>(null);
  const questParam = readQuestParam();
  const resolvedSource = questParam
    ? safeResolve(questParam, appConfig.defaultQuestLibraryBaseUrl)
    : null;

  useEffect(() => {
    if (!questParam) {
      return;
    }

    let cancelled = false;

    async function loadFromQuery() {
      setLoading(true);
      setError(null);
      try {
        const document = await loadQuestFromRequest(questParam, {
          defaultBaseUrl: appConfig.defaultQuestLibraryBaseUrl
        });
        if (!cancelled) {
          setLoadedQuest(document);
        }
      } catch (loadError) {
        if (!cancelled) {
          setError(loadError instanceof Error ? loadError.message : "Failed to load quest.");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void loadFromQuery();

    return () => {
      cancelled = true;
    };
  }, [questParam]);

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

  return (
    <main className="shell">
      <section className="hero">
        <p className="eyebrow">Web Player</p>
        <h1>FireURQ3</h1>
      </section>

      {loadedQuest ? (
        <PlayerErrorBoundary>
          <Suspense
            fallback={
              <section className="panel player-panel">
                <div className="player-header">
                  <div>
                    <p className="eyebrow">Player</p>
                    <h2>{describeSource(loadedQuest)}</h2>
                  </div>
                </div>
                <div className="resolution">
                  <span className="resolution-label">Status</span>
                  <span className="mono">Loading graphical player...</span>
                </div>
              </section>
            }
          >
            <GraphicalPlayer quest={loadedQuest} />
          </Suspense>
        </PlayerErrorBoundary>
      ) : (
        <section className="panel">
          {questParam ? (
            <>
              {resolvedSource ? (
                <div className="resolution">
                  <span className="resolution-label">Resolved Query Source</span>
                  <span className="mono">{questParam}</span>
                  <span className="mono">{resolvedSource.url}</span>
                </div>
              ) : (
                <div className="resolution">
                  <span className="resolution-label">Query Source</span>
                  <span className="mono">{questParam}</span>
                </div>
              )}

              {loading ? (
                <div className="resolution">
                  <span className="resolution-label">Status</span>
                  <span className="mono">Loading quest...</span>
                </div>
              ) : null}

              {error ? <p className="error">{error}</p> : null}
            </>
          ) : (
            <>
              <label
                className="dropzone"
                onDragOver={(event) => event.preventDefault()}
                onDrop={handleDrop}
              >
                <input type="file" accept=".qst,.txt" onChange={handleFileInput} />
                <span>Drop a local quest here or choose a file from the device</span>
              </label>

              {loading ? (
                <div className="resolution">
                  <span className="resolution-label">Status</span>
                  <span className="mono">Loading quest...</span>
                </div>
              ) : null}

              {error ? <p className="error">{error}</p> : null}
            </>
          )}
        </section>
      )}
    </main>
  );
}

function describeSource(document: LoadedQuestDocument): string {
  return document.source.kind === "file" ? document.source.name : document.source.url;
}

function safeResolve(request: string, defaultBaseUrl: string) {
  try {
    return resolveQuestSource(request, { defaultBaseUrl });
  } catch {
    return null;
  }
}

function readQuestParam(): string {
  if (typeof window === "undefined") {
    return "";
  }

  return new URLSearchParams(window.location.search).get("quest")?.trim() ?? "";
}
