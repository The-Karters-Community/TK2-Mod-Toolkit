import { Component, StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App.jsx";
import { ToolkitProvider } from "./lib/useToolkit.jsx";
import "./styles.css";

class ErrorBoundary extends Component {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    return this.state.failed ? (
      <div className="p-8">
        <h1>The interface could not open</h1>
        <p className="mt-3">
          Your files are unchanged. Reload the application to try again.
        </p>
        <button className="button mt-4" onClick={() => location.reload()}>
          Reload
        </button>
      </div>
    ) : (
      this.props.children
    );
  }
}

createRoot(document.getElementById("root")).render(
  <StrictMode>
    <ErrorBoundary>
      <ToolkitProvider>
        <App />
      </ToolkitProvider>
    </ErrorBoundary>
  </StrictMode>,
);
