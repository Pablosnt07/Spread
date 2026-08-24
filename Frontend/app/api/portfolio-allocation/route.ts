const spreadApiUrl = process.env.SPREAD_API_URL ?? "http://127.0.0.1:5087";

export async function POST(request: Request) {
  try {
    const body = await request.text();
    const response = await fetch(`${spreadApiUrl}/api/portfolios/allocation-preview`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body,
      cache: "no-store",
      signal: AbortSignal.timeout(5000),
    });

    return new Response(await response.text(), {
      status: response.status,
      headers: { "content-type": response.headers.get("content-type") ?? "application/json" },
    });
  } catch {
    return Response.json(
      { title: "Portfolio service unavailable" },
      { status: 503 },
    );
  }
}
