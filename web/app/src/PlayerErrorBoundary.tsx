import { Component, type ErrorInfo, type ReactNode } from "react";

interface Props {
  children: ReactNode;
}

interface State {
  error: Error | null;
}

export class PlayerErrorBoundary extends Component<Props, State> {
  state: State = {
    error: null
  };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    console.error("Graphical player failed to render.", error, errorInfo);
  }

  render() {
    if (this.state.error) {
      return (
        <section className="panel player-panel">
          <div className="player-header">
            <div>
              <p className="eyebrow">Player</p>
              <h2>Graphical Player Error</h2>
            </div>
          </div>
          <div className="resolution">
            <span className="resolution-label">Status</span>
            <span className="mono">{this.state.error.message}</span>
          </div>
          <p className="error">
            If this happened right after installing or changing Pixi dependencies, restart `npm run dev`
            and refresh the page.
          </p>
        </section>
      );
    }

    return this.props.children;
  }
}
