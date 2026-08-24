import { CompanyAnalysis } from "@/components/CompanyAnalysis";
import { Nav } from "@/components/Nav";

export default async function CompanyPage({ params }: { params: Promise<{ ticker: string }> }) {
  const { ticker } = await params;
  return <><Nav /><CompanyAnalysis ticker={ticker.toUpperCase()} /></>;
}
