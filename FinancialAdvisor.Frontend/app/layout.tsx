import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import { Header } from "./components/Header";
import { ChatWidget } from "./components/ChatWidget";
import { UIProvider } from "./context/UIContext";

const inter = Inter({ subsets: ["latin"] });

export const metadata: Metadata = {
  title: "Financial Advisor AI",
  description: "Your AI-powered financial assistant",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className={`${inter.className} bg-zinc-50 dark:bg-[#242526] text-zinc-900 dark:text-zinc-100 min-h-screen flex flex-col`} suppressHydrationWarning>
        <UIProvider>
          <Header />
          <main className="flex-1 flex flex-col relative pb-24"> 
            {/* Main content area padding bottom accounts for the chat widget */}
        {children}
            <ChatWidget />
          </main>
        </UIProvider>
      </body>
    </html>
  );
}
