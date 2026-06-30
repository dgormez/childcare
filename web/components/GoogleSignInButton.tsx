"use client";
import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "./AuthProvider";
import { setAccessToken } from "../lib/api";
import type { AuthResponse } from "../lib/types";

const CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID ?? "";
const API_BASE  = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

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

export default function GoogleSignInButton() {
  const router        = useRouter();
  const { setSession } = useAuth();
  const buttonRef     = useRef<HTMLDivElement>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!CLIENT_ID) return;

    const initButton = () => {
      window.google!.accounts.id.initialize({
        client_id: CLIENT_ID,
        callback:  handleCredential,
      });
      if (buttonRef.current) {
        window.google!.accounts.id.renderButton(buttonRef.current, {
          type:  "standard",
          theme: "outline",
          size:  "large",
          width: 352,
          text:  "continue_with",
        });
      }
    };

    if (window.google?.accounts) {
      initButton();
      return;
    }

    const script = document.createElement("script");
    script.src   = "https://accounts.google.com/gsi/client";
    script.async = true;
    script.defer = true;
    script.onload = initButton;
    document.body.appendChild(script);

    return () => { document.body.removeChild(script); };
  }, []);

  const handleCredential = async (response: { credential: string }) => {
    setError("");
    try {
      const res = await fetch(`${API_BASE}/api/auth/google`, {
        method:  "POST",
        headers: { "Content-Type": "application/json" },
        body:    JSON.stringify({ idToken: response.credential }),
      });

      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error((body as { error?: string }).error ?? "Google sign-in failed.");
      }

      const data: AuthResponse = await res.json();

      await fetch("/api/set-refresh-token", {
        method:  "POST",
        headers: { "Content-Type": "application/json" },
        body:    JSON.stringify({ refreshToken: data.refreshToken }),
      });

      setAccessToken(data.accessToken);
      setSession(data);
      router.replace("/habits");
    } catch (e) {
      setError((e as Error).message ?? "Google sign-in failed.");
    }
  };

  if (!CLIENT_ID) return null;

  return (
    <div className="space-y-2">
      <div ref={buttonRef} className="flex justify-center" />
      {error && <p className="text-red-500 text-sm text-center">{error}</p>}
    </div>
  );
}
