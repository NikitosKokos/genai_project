<<<<<<< Current (Your changes)
=======
import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import { Header } from "./components/Header";
import { ChatWidget } from "./components/ChatWidget";

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
    <html lang="en">
      <body className={`${inter.className} bg-zinc-50 dark:bg-zinc-950 text-zinc-900 dark:text-zinc-100 min-h-screen flex flex-col`}>
        <Header />
        <main className="flex-1 flex flex-col relative pb-[520px]"> 
          {/* Main content area padding bottom accounts for the chat widget height */}
          {children}
          <ChatWidget />
        </main>
      </body>
    </html>
  );
}
>>>>>>> Incoming (Background Agent changes)
