import type { Metadata } from "next";
import { Public_Sans } from "next/font/google";
import { NextIntlClientProvider } from "next-intl";
import { getLocale } from "next-intl/server";
import { Toaster } from "react-hot-toast";
import "./globals.css";
import { AuthProvider } from "../components/AuthProvider";

// design-system.md Typography: Public Sans, one family across the full weight range,
// used for everything — not the browser default sans-serif.
const publicSans = Public_Sans({
  subsets: ["latin"],
  weight: ["300", "400", "500", "600", "700", "800"],
  variable: "--font-public-sans",
});

export const metadata: Metadata = {
  title:       "ChildCare Admin",
  description: "ChildCare director web admin.",
};

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const locale = await getLocale();

  return (
    <html lang={locale} className={publicSans.variable}>
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
