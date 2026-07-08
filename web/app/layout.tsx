import type { Metadata } from "next";
import { NextIntlClientProvider } from "next-intl";
import { getLocale } from "next-intl/server";
import { Toaster } from "react-hot-toast";
import "./globals.css";
import { AuthProvider } from "../components/AuthProvider";

export const metadata: Metadata = {
  title:       "ChildCare Admin",
  description: "ChildCare director web admin.",
};

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const locale = await getLocale();

  return (
    <html lang={locale}>
      <body className="bg-background text-text antialiased dark:bg-background-dark dark:text-text-dark">
        <NextIntlClientProvider>
          <AuthProvider>
            {children}
          </AuthProvider>
          <Toaster position="bottom-center" toastOptions={{ duration: 4000 }} />
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
