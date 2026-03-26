import { useEffect, useRef, useState } from "react";
import { Application, Container, Graphics, Text, TextStyle } from "pixi.js";
import { PlayerSession, type LoadedQuestDocument } from "@fireurq/player";
import type { FrameButton, PlayerFrame } from "@fireurq/player";

const backgroundColor = 0x10131a;
const menuBarColor = 0x1d1313;
const menuBarAccentColor = 0xffb28a;
const panelBorderColor = 0x2d3341;
const textColor = 0xe2e8f0;
const accentColor = 0xffb28a;
const buttonColor = 0x253247;
const buttonHoverColor = 0x314564;
const buttonTextColor = 0xf8fafc;
const inventoryIconColor = 0xf4efe8;

const layout = {
  viewportWidth: 800,
  viewportHeight: 600,
  menuBarHeight: 46,
  textPaneX: 20,
  textPaneY: 55,
  textPaneWidth: 760,
  textPaneHeight: 525,
  textInsetX: 0,
  textInsetTop: 0,
  textInsetBottom: 0,
  buttonInsetX: 2
} as const;

export function GraphicalPlayer(props: { quest: LoadedQuestDocument }) {
  const hostRef = useRef<HTMLDivElement | null>(null);
  const appRef = useRef<Application | null>(null);
  const sessionRef = useRef<PlayerSession | null>(null);
  const frameRef = useRef<PlayerFrame | null>(null);
  const scrollMetricsRef = useRef<{ contentHeight: number; viewportHeight: number }>({
    contentHeight: 0,
    viewportHeight: 0
  });
  const scrollOffsetRef = useRef(0);
  const [frame, setFrame] = useState<PlayerFrame | null>(null);
  const [viewSize, setViewSize] = useState({ width: 0, height: 0 });
  const [appReady, setAppReady] = useState(false);
  const [scrollOffset, setScrollOffset] = useState(0);

  useEffect(() => {
    const host = hostRef.current;
    if (!host) {
      return;
    }

    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (!entry) {
        return;
      }

      const width = Math.max(1, Math.round(entry.contentRect.width));
      const height = Math.max(360, Math.round(entry.contentRect.height));
      setViewSize((current) =>
        current.width === width && current.height === height
          ? current
          : { width, height }
      );
    });

    observer.observe(host);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const host = hostRef.current;
    if (!host) {
      return;
    }

    const handleWheel = (event: WheelEvent) => {
      const metrics = scrollMetricsRef.current;
      if (metrics.contentHeight <= 0 || metrics.viewportHeight <= 0) {
        return;
      }

      const maxScroll = Math.max(0, metrics.contentHeight - (metrics.viewportHeight - 24));
      if (maxScroll <= 0 || event.deltaY === 0) {
        return;
      }

      event.preventDefault();
      event.stopPropagation();

      const next = Math.max(0, Math.min(maxScroll, scrollOffsetRef.current + event.deltaY));
      if (next !== scrollOffsetRef.current) {
        scrollOffsetRef.current = next;
        setScrollOffset(next);
      }
    };

    host.addEventListener("wheel", handleWheel, { passive: false });
    return () => host.removeEventListener("wheel", handleWheel);
  }, []);

  useEffect(() => {
    let disposed = false;

    async function setupApp() {
      const host = hostRef.current;
      if (!host) {
        return;
      }

      const app = new Application();
      await app.init({
        backgroundAlpha: 0,
        antialias: true,
        resizeTo: host,
        autoDensity: true
      });

      if (disposed) {
        await app.destroy(true);
        return;
      }

      host.appendChild(app.canvas);
      appRef.current = app;
      setAppReady(true);
    }

    void setupApp();

    return () => {
      disposed = true;
      setAppReady(false);
      const app = appRef.current;
      appRef.current = null;
      if (app) {
        void app.destroy(true);
      }
    };
  }, []);

  useEffect(() => {
    const session = new PlayerSession();
    session.load(props.quest);
    sessionRef.current = session;
    frameRef.current = null;
    setFrame(null);
    scrollOffsetRef.current = 0;
    setScrollOffset(0);
  }, [props.quest]);

  useEffect(() => {
    frameRef.current = frame;
  }, [frame]);

  useEffect(() => {
    scrollOffsetRef.current = scrollOffset;
  }, [scrollOffset]);

  useEffect(() => {
    const session = sessionRef.current;
    if (!session || viewSize.width <= 0 || viewSize.height <= 0) {
      return;
    }

    setFrame((current) =>
      current ? session.snapshot(viewSize.width, viewSize.height) : session.advance(viewSize.width, viewSize.height)
    );
  }, [viewSize.width, viewSize.height]);

  useEffect(() => {
    const app = appRef.current;
    if (!app || !appReady || !frame) {
      return;
    }

    const root = new Container();
    const frameContainer = new Container();
    frameContainer.position.set(frame.viewTransform.offsetX, frame.viewTransform.offsetY);
    frameContainer.scale.set(frame.viewTransform.scale);

    root.addChild(frameContainer);

    frameContainer.addChild(buildBackdrop(frame));
    const contentFlow = buildContentFlow(
      frame,
      scrollOffset,
      (buttonId) => {
        const session = sessionRef.current;
        if (!session) {
          return;
        }

        setFrame(session.selectButton(buttonId, viewSize.width, viewSize.height));
      }
    );
    scrollMetricsRef.current = {
      contentHeight: contentFlow.contentHeight,
      viewportHeight: contentFlow.viewportHeight
    };
    frameContainer.addChild(contentFlow.container);

    app.stage.removeChildren();
    app.stage.addChild(root);

    return () => {
      root.destroy({ children: true });
    };
  }, [appReady, frame, props.quest, scrollOffset, viewSize.height, viewSize.width]);

  return (
    <section className="player-shell">
      <div ref={hostRef} className="player-canvas-host" />
    </section>
  );
}

function buildBackdrop(frame: PlayerFrame): Graphics {
  const graphics = new Graphics();
  graphics.rect(0, 0, frame.virtualWidth, frame.virtualHeight).fill(backgroundColor);

  graphics.rect(0, 0, frame.virtualWidth, layout.menuBarHeight).fill(menuBarColor);

  graphics.roundRect(670, 6, 110, 34, 12).stroke({
    color: panelBorderColor,
    width: 1
  });
  graphics.moveTo(0, layout.textPaneY).lineTo(frame.virtualWidth, layout.textPaneY).stroke({
    color: panelBorderColor,
    width: 1
  });
  graphics.moveTo(106, 29).lineTo(671, 29).stroke({
    color: panelBorderColor,
    width: 2
  });

  return graphics;
}

function buildContentFlow(
  frame: PlayerFrame,
  scrollOffset: number,
  onSelect: (buttonId: number) => void
): { container: Container; contentHeight: number; viewportHeight: number } {
  const container = new Container();
  const content = new Container();
  const clipTop = layout.textPaneY;
  const clipLeft = layout.textPaneX;
  const clipWidth = layout.textPaneWidth;
  const clipHeight = layout.textPaneHeight;

  const mask = new Graphics();
  mask.rect(clipLeft, clipTop, clipWidth, clipHeight).fill(0xffffff);
  container.addChild(mask);
  container.addChild(content);
  content.mask = mask;

  const menuLabel = new Text({
    text: "fireURQ",
    style: new TextStyle({
      fontFamily: "\"Georgia\", serif",
      fontSize: 20,
      fontWeight: "700",
      fill: menuBarAccentColor
    })
  });
  menuLabel.position.set(22, 11);
  container.addChild(menuLabel);

  const inventoryLabel = new Text({
    text: "Инв.",
    style: new TextStyle({
      fontFamily: "\"Segoe UI\", sans-serif",
      fontSize: 14,
      fontWeight: "600",
      fill: inventoryIconColor
    })
  });
  inventoryLabel.position.set(715, 14);
  container.addChild(inventoryLabel);

  const inventoryIcon = new Graphics();
  inventoryIcon.roundRect(684, 12, 20, 16, 4).stroke({
    color: inventoryIconColor,
    width: 2
  });
  inventoryIcon.moveTo(688, 12).lineTo(692, 8).lineTo(700, 8).lineTo(704, 12).stroke({
    color: inventoryIconColor,
    width: 2
  });
  container.addChild(inventoryIcon);

  const transcriptText = normalizeTranscript(frame);
  const transcript = new Text({
    text: transcriptText || "Quest loaded. Waiting for first frame output.",
    style: new TextStyle({
      fontFamily: "\"Segoe UI\", sans-serif",
      fontSize: 16,
      lineHeight: 24,
      fill: textColor,
      wordWrap: true,
      breakWords: true,
      wordWrapWidth: clipWidth - layout.textInsetX * 2
    })
  });
  transcript.position.set(
    clipLeft + layout.textInsetX,
    clipTop + layout.textInsetTop
  );
  content.addChild(transcript);

  let contentHeight = transcript.y + transcript.height;

  if (frame.buttons.length === 0) {
    content.y = -clampScroll(scrollOffset, contentHeight, clipHeight);
    return {
      container,
      contentHeight,
      viewportHeight: clipHeight
    };
  }

  const buttonWidth = clipWidth - layout.textInsetX * 2 - layout.buttonInsetX * 2;
  const buttonHeight = 48;
  const buttonGap = 16;
  const startY = contentHeight + 16;

  frame.buttons.forEach((button, index) => {
    const buttonContainer = buildButton(
      button,
      clipLeft + layout.textInsetX + layout.buttonInsetX,
      startY + index * (buttonHeight + buttonGap),
      buttonWidth,
      buttonHeight,
      onSelect
    );
    content.addChild(buttonContainer);
  });

  contentHeight =
    startY +
    (frame.buttons.length - 1) * (buttonHeight + buttonGap) +
    buttonHeight +
    layout.textInsetBottom;
  content.y = -clampScroll(scrollOffset, contentHeight, clipHeight);

  return {
    container,
    contentHeight,
    viewportHeight: clipHeight
  };
}

function buildButton(
  button: FrameButton,
  x: number,
  y: number,
  width: number,
  height: number,
  onSelect: (buttonId: number) => void
): Container {
  const container = new Container();
  container.position.set(x, y);
  container.eventMode = "static";
  container.cursor = "pointer";

  const background = new Graphics();
  drawButtonBackground(background, width, height, false);
  container.addChild(background);

  const label = new Text({
    text: button.caption,
    style: new TextStyle({
      fontFamily: "\"Segoe UI\", sans-serif",
      fontSize: 16,
      fontWeight: "700",
      fill: buttonTextColor,
      wordWrap: true,
      breakWords: true,
      wordWrapWidth: width - 32
    })
  });
  label.position.set(16, Math.max(8, (height - label.height) * 0.5));
  container.addChild(label);

  container.on("pointertap", () => onSelect(button.id));
  container.on("pointerover", () => drawButtonBackground(background, width, height, true));
  container.on("pointerout", () => drawButtonBackground(background, width, height, false));

  return container;
}

function drawButtonBackground(graphics: Graphics, width: number, height: number, hovered: boolean) {
  graphics.clear();
  graphics.roundRect(0, 0, width, height, 16).fill(hovered ? buttonHoverColor : buttonColor);
  graphics.roundRect(0, 0, width, height, 16).stroke({
    color: hovered ? accentColor : panelBorderColor,
    width: hovered ? 2 : 1
  });
}

function normalizeTranscript(frame: PlayerFrame): string {
  const joined = frame.textRuns.map((run) => run.text).join("").trim();
  if (!joined) {
    return "";
  }

  return joined.replace(/\n{3,}/gu, "\n\n");
}

function clampScroll(current: number, contentHeight: number, viewportHeight: number): number {
  const maxScroll = Math.max(0, contentHeight - viewportHeight);
  return Math.max(0, Math.min(maxScroll, current));
}
