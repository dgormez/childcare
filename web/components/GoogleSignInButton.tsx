"use client";
import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useAuth } from "./AuthProvider";
import { apiClient } from "../lib/apiClient";
import { completeGoogleSignIn } from "../lib/auth";
import type { AuthResponse } from "../lib/types";

const CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID ?? "";

declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (config: object) => void;
          renderButton: (el: HTMLElement, options: object) => void;
        };
      };
    };
  }
}

export default function GoogleSignInButton({ organisationSlug }: { organisationSlug: string }) {
  const t = useTranslations("login");
  const router = useRouter();
  const { setSession } = useAuth();
  const buttonRef = useRef<HTMLDivElement>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!CLIENT_ID || !organisationSlug) return;

    const initButton = () => {
      window.google!.accounts.id.initialize({
        client_id: CLIENT_ID,
        callback: handleCredential,
      });
      if (buttonRef.current) {
        window.google!.accounts.id.renderButton(buttonRef.current, {
          type: "standard",
          theme: "outline",
          size: "large",
          width: 352,
          text: "continue_with",
        });
      }
    };

    if (window.google?.accounts) {
      initButton();
      return;
    }

    const script = document.createElement("script");
    script.src = "https://accounts.google.com/gsi/client";
    script.async = true;
    script.defer = true;
    script.onload = initButton;
    document.body.appendChild(script);

    return () => {
      document.body.removeChild(script);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [organisationSlug]);

  const handleCredential = async (response: { credential: string }) => {
    setError("");
    try {
      const result = await apiClient.POST("/api/auth/google", {
        body: { organisationSlug, idToken: response.credential },
      });

      if (!result.response.ok) {
        const errorKey = (result.error as { errorKey?: string } | undefined)?.errorKey;
        throw new Error(errorKey ?? "errors.auth.invalid_credentials");
      }

      const data = result.data as unknown as AuthResponse;
      const session = await completeGoogleSignIn(data, organisationSlug);
      setSession(session);
      router.replace("/staff");
    } catch {
      setError(t("googleSignInError"));
    }
  };

  if (!CLIENT_ID) return null;

  return (
    <div className="space-y-2">
      <div ref={buttonRef} className="flex justify-center" />
      {error && <p className="text-sm text-danger text-center">{error}</p>}
    </div>
  );
}
