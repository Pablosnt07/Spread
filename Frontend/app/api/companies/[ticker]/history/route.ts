import { NextResponse } from "next/server";

const TICKER_PATTERN = /^[A-Z0-9.-]{1,12}$/;
const RANGES = new Set(["ytd", "1y", "3y", "5y", "max"]);
const backendUrl = process.env.SPREAD_API_URL ?? "http://127.0.0.1:5087";

export async function GET(request: Request, { params }: { params: Promise<{ ticker: string }> }) {
  const { ticker: rawTicker } = await params;
  const ticker = rawTicker.trim().toUpperCase();
  const range = new URL(request.url).searchParams.get("range")?.toLowerCase() ?? "5y";
  if (!TICKER_PATTERN.test(ticker) || !RANGES.has(range)) {
    return NextResponse.json({ detail: "Ticker o rango inválido." }, { status: 400 });
  }
  try {
    const response = await fetch(`${backendUrl}/api/companies/${encodeURIComponent(ticker)}/history?range=${encodeURIComponent(range)}`, { cache: "no-store", signal: AbortSignal.timeout(12_000) });
    const body = await response.text();
    return new NextResponse(body, { status: response.status, headers: { "content-type": response.headers.get("content-type") ?? "application/json", "cache-control": response.ok ? "public, max-age=300, stale-while-revalidate=3600" : "no-store" } });
  } catch {
    return NextResponse.json({ detail: "El histórico no está disponible temporalmente." }, { status: 503 });
  }
}
