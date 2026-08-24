import "server-only";
import { NextRequest, NextResponse } from "next/server";

const SEARCH_PATTERN = /^[A-Za-z0-9 .&'-]{2,64}$/;
const backendUrl = process.env.SPREAD_API_URL ?? "http://127.0.0.1:5087";
const buckets = new Map<string, { count: number; resetAt: number }>();
const WINDOW_MS = 60_000;
const PERMIT_LIMIT = 12;
const MAX_BUCKETS = 2_000;

function getClientKey(request: NextRequest) {
  return request.headers.get("x-vercel-forwarded-for")?.split(",")[0]?.trim()
    ?? request.headers.get("x-forwarded-for")?.split(",")[0]?.trim()
    ?? "unknown";
}

function isRateLimited(key: string) {
  const now = Date.now();
  const current = buckets.get(key);
  if (!current || current.resetAt <= now) {
    if (buckets.size >= MAX_BUCKETS) {
      for (const [storedKey, bucket] of buckets) if (bucket.resetAt <= now) buckets.delete(storedKey);
      if (buckets.size >= MAX_BUCKETS) buckets.delete(buckets.keys().next().value ?? "");
    }
    buckets.set(key, { count: 1, resetAt: now + WINDOW_MS });
    return false;
  }
  current.count += 1;
  return current.count > PERMIT_LIMIT;
}

export async function GET(request: NextRequest) {
  const query = request.nextUrl.searchParams.get("q")?.trim().replace(/\s+/g, " ") ?? "";
  const limit = Number(request.nextUrl.searchParams.get("limit") ?? "6");
  if (!SEARCH_PATTERN.test(query) || !Number.isInteger(limit) || limit < 1 || limit > 8) {
    return NextResponse.json({ detail: "Parámetros de búsqueda inválidos." }, { status: 400 });
  }
  if (isRateLimited(getClientKey(request))) {
    return NextResponse.json({ detail: "Demasiadas búsquedas. Esperá un minuto." }, { status: 429, headers: { "retry-after": "60" } });
  }

  try {
    const response = await fetch(`${backendUrl}/api/companies/search?q=${encodeURIComponent(query)}&limit=${limit}`, { cache: "no-store", signal: AbortSignal.timeout(8_000) });
    const body = await response.text();
    return new NextResponse(body, {
      status: response.status,
      headers: { "content-type": response.headers.get("content-type") ?? "application/json", ...(response.headers.get("retry-after") ? { "retry-after": response.headers.get("retry-after")! } : {}) },
    });
  } catch {
    return NextResponse.json({ detail: "La búsqueda no está disponible temporalmente." }, { status: 503 });
  }
}
