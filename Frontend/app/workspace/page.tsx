import { Nav } from "@/components/Nav";
import { Workspace } from "@/components/Workspace";

export default async function WorkspacePage({ searchParams }: { searchParams: Promise<{ tab?: string }> }) {
  const params = await searchParams;
  return <><Nav active={params.tab ?? "portfolio"} /><Workspace initialTab={params.tab} /></>;
}
