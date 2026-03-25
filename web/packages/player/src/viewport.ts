import type { ViewTransform } from "./player-frame.js";

export class ViewportMapper {
  static computeLetterbox(
    virtualWidth: number,
    virtualHeight: number,
    viewWidth: number,
    viewHeight: number
  ): ViewTransform {
    if (virtualWidth <= 0 || virtualHeight <= 0 || viewWidth <= 0 || viewHeight <= 0) {
      return {
        virtualWidth,
        virtualHeight,
        viewWidth,
        viewHeight,
        scale: 1,
        offsetX: 0,
        offsetY: 0
      };
    }

    const sx = viewWidth / virtualWidth;
    const sy = viewHeight / virtualHeight;
    const scale = Math.min(sx, sy);
    const usedWidth = virtualWidth * scale;
    const usedHeight = virtualHeight * scale;

    return {
      virtualWidth,
      virtualHeight,
      viewWidth,
      viewHeight,
      scale,
      offsetX: (viewWidth - usedWidth) * 0.5,
      offsetY: (viewHeight - usedHeight) * 0.5
    };
  }

  static mapViewToVirtual(
    transform: ViewTransform,
    viewX: number,
    viewY: number
  ): { x: number; y: number; inside: boolean } {
    const x = (viewX - transform.offsetX) / transform.scale;
    const y = (viewY - transform.offsetY) / transform.scale;
    const inside =
      x >= 0 &&
      y >= 0 &&
      x <= transform.virtualWidth &&
      y <= transform.virtualHeight;

    return { x, y, inside };
  }
}
