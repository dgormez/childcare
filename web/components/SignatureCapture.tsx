"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "./ui/tabs";
import { Input } from "./ui/input";
import { Button } from "./ui/button";

export type SignatureType = "Drawn" | "Typed";
export interface SignatureValue {
  signatureType: SignatureType;
  signatureData: string;
}

interface SignatureCaptureProps {
  onChange: (value: SignatureValue | null) => void;
}

const CANVAS_WIDTH = 480;
const CANVAS_HEIGHT = 160;

/**
 * Feature 024-esignature (User Story 1, FR-006). Two first-class input modes — draw (Pointer
 * Events, works for touch/mouse/pen alike) or type — per design-system.md's accessibility
 * requirement that a keyboard-only parent must still be able to complete signing: the "Type"
 * tab is a plain text input, not a fallback bolted onto a canvas-only design.
 */
export function SignatureCapture({ onChange }: SignatureCaptureProps) {
  const t = useTranslations("contractSigning");
  const [mode, setMode] = useState<SignatureType>("Drawn");
  const [typedName, setTypedName] = useState("");
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const drawingRef = useRef(false);
  const hasDrawnRef = useRef(false);

  const clearCanvas = useCallback(() => {
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext("2d");
    ctx?.clearRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
    hasDrawnRef.current = false;
    onChange(null);
  }, [onChange]);

  useEffect(() => {
    // Switching modes must not let a stale value from the other mode survive into submission.
    if (mode === "Typed") {
      onChange(typedName.trim() ? { signatureType: "Typed", signatureData: typedName.trim() } : null);
    } else {
      onChange(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode]);

  function handleTypedChange(value: string) {
    setTypedName(value);
    onChange(value.trim() ? { signatureType: "Typed", signatureData: value.trim() } : null);
  }

  function pointFromEvent(canvas: HTMLCanvasElement, e: React.PointerEvent<HTMLCanvasElement>) {
    const rect = canvas.getBoundingClientRect();
    return {
      x: ((e.clientX - rect.left) / rect.width) * CANVAS_WIDTH,
      y: ((e.clientY - rect.top) / rect.height) * CANVAS_HEIGHT,
    };
  }

  function handlePointerDown(e: React.PointerEvent<HTMLCanvasElement>) {
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext("2d");
    if (!canvas || !ctx) return;
    canvas.setPointerCapture(e.pointerId);
    const { x, y } = pointFromEvent(canvas, e);
    ctx.beginPath();
    ctx.moveTo(x, y);
    drawingRef.current = true;
  }

  function handlePointerMove(e: React.PointerEvent<HTMLCanvasElement>) {
    if (!drawingRef.current) return;
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext("2d");
    if (!canvas || !ctx) return;
    const { x, y } = pointFromEvent(canvas, e);
    ctx.lineWidth = 2;
    ctx.lineCap = "round";
    ctx.strokeStyle = "#1F2937";
    ctx.lineTo(x, y);
    ctx.stroke();
    hasDrawnRef.current = true;
  }

  function handlePointerUp() {
    drawingRef.current = false;
    const canvas = canvasRef.current;
    if (!canvas || !hasDrawnRef.current) return;
    onChange({ signatureType: "Drawn", signatureData: canvas.toDataURL("image/png") });
  }

  return (
    <Tabs value={mode} onValueChange={(v) => setMode(v as SignatureType)}>
      <TabsList>
        <TabsTrigger value="Drawn">{t("signatureDrawTab")}</TabsTrigger>
        <TabsTrigger value="Typed">{t("signatureTypeTab")}</TabsTrigger>
      </TabsList>

      <TabsContent value="Drawn">
        <p className="mb-2 text-xs text-text-soft dark:text-text-soft-dark">{t("signatureDrawHint")}</p>
        <canvas
          ref={canvasRef}
          width={CANVAS_WIDTH}
          height={CANVAS_HEIGHT}
          className="w-full touch-none rounded-lg bg-surface-soft dark:bg-surface-soft-dark"
          onPointerDown={handlePointerDown}
          onPointerMove={handlePointerMove}
          onPointerUp={handlePointerUp}
          onPointerLeave={handlePointerUp}
          role="img"
          aria-label={t("signatureDrawHint")}
        />
        <Button type="button" variant="secondary" size="sm" className="mt-2" onClick={clearCanvas}>
          {t("signatureClear")}
        </Button>
      </TabsContent>

      <TabsContent value="Typed">
        <label className="block text-sm font-medium text-text dark:text-text-dark">
          {t("signatureTypeTab")}
          <Input
            className="mt-2"
            value={typedName}
            onChange={(e) => handleTypedChange(e.target.value)}
            placeholder={t("signatureTypePlaceholder")}
          />
        </label>
      </TabsContent>
    </Tabs>
  );
}
