import { WhatsNewPanel } from "./components/WhatsNewPanel";
import { PortfolioPanel } from "./components/PortfolioPanel";
import { AssetsPanel } from "./components/AssetsPanel";

export default function Home() {
  return (
    <div className="container mx-auto p-6 max-w-7xl h-full">
      <div className="grid grid-cols-1 md:grid-cols-12 gap-6 h-full">
        {/* Left Column: What's New & Assets */}
        <div className="md:col-span-4 flex flex-col gap-6">
          <div className="min-h-[320px]">
            <WhatsNewPanel />
          </div>
          <div className="flex-1">
            <AssetsPanel />
          </div>
        </div>

        {/* Right Column: Portfolio */}
        <div className="md:col-span-8 h-full">
          <PortfolioPanel />
        </div>
      </div>
    </div>
  );
}
