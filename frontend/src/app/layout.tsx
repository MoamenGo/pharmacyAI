import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "PharmaAI",
  description: "نظام إدارة الصيدليات",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="ar" dir="rtl" className="dark">
      <body className="min-h-screen font-sans">{children}</body>
    </html>
  );
}
