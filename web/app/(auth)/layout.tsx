"use client";
import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useAuth } from "../../components/AuthProvider";

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  const { session, loading } = useAuth();
  const router = useRouter();
  const t = useTranslations("login");

  useEffect(() => {
    if (!loading && session) router.replace("/staff");
  }, [loading, session, router]);

  return (
    <div className="min-h-screen flex items-center justify-center px-4 bg-background dark:bg-background-dark">
      <div className="w-full max-w-sm">
        <div className="text-center mb-8">
          <h1 className="text-3xl font-bold text-text dark:text-text-dark">{t("productName")}</h1>
          <p className="text-text-soft dark:text-text-soft-dark text-sm mt-1">{t("productTagline")}</p>
        </div>
        {children}
      </div>
    </div>
  );
}
