import { NextResponse } from "next/server";

const TICKER_PATTERN = /^[A-Z0-9.-]{1,12}$/;
const backendUrl = process.env.SPREAD_API_URL ?? "http://127.0.0.1:5087";

export async function GET(
  _request: Request,
  { params }: { params: Promise<{ ticker: string }> },
) {
  const { ticker: rawTicker } = await params;
  const ticker = rawTicker.trim().toUpperCase();

  if (!TICKER_PATTERN.test(ticker)) {
    return NextResponse.json({ detail: "Ticker inválido." }, { status: 400 });
  }

  try {
    const response = await fetch(
      `${backendUrl}/api/companies/${encodeURIComponent(ticker)}/financials`,
      { cache: "no-store", signal: AbortSignal.timeout(15_000) },
    );
    const body = await response.text();
    return new NextResponse(body, {
      status: response.status,
      headers: { "content-type": response.headers.get("content-type") ?? "application/json" },
    });
  } catch {
    return NextResponse.json(
      { detail: "El estado de resultados no está disponible temporalmente." },
      { status: 503 },
    );
  }
}
