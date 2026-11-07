"use client";

import { useState, useEffect, useRef } from "react";
import { AnimatePresence, motion, useAnimationControls } from "framer-motion";

const CheckIcon = ({ size = 16, strokeWidth = 3, ...props }) => (
  <svg
    xmlns="http://www.w3.org/2000/svg"
    width={size}
    height={size}
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth={strokeWidth}
    strokeLinecap="round"
    strokeLinejoin="round"
    {...props}
  >
    <path d="M20 6 9 17l-5-5" />
  </svg>
);

const OTPSuccess = () => {
  return (
    <div className="flex items-center justify-center gap-4 w-full">
      <motion.div
        initial={{ opacity: 0, scale: 0.5 }}
        animate={{ opacity: 1, scale: 1 }}
        transition={{ delay: 0.3, type: "spring", stiffness: 500, damping: 30 }}
        className="w-16 h-16 bg-emerald-500 ring-4 ring-emerald-400/30 text-white flex items-center justify-center rounded-full shadow-lg"
      >
        <CheckIcon size={32} strokeWidth={3} />
      </motion.div>
      <motion.p
        initial={{ opacity: 0, x: -10 }}
        animate={{ opacity: 1, x: 0 }}
        transition={{ delay: 0.4, duration: 0.4 }}
        className="text-emerald-300 font-semibold text-lg"
      >
        Email verified!
      </motion.p>
    </div>
  );
};

// [Pharmino] `message` lets the server's reason ("2 tries left", "expired") show; the default is the original text.
const OTPError = ({ message = "Invalid OTP. Please try again." }: { message?: string }) => {
  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -10 }}
      transition={{ duration: 0.2 }}
      className="text-center text-red-300 text-sm font-medium mt-2 absolute -bottom-8 w-full"
    >
      {message}
    </motion.div>
  );
};

const OTPInputBox = ({
  index,
  verifyOTP,
  state,
  stiffness = 700,
  damping = 20,
  y = 10,
  opacity = 0,
}) => {
  const animationControls = useAnimationControls();
  const springTransition = {
    type: "spring",
    stiffness,
    damping,
    delay: index * 0.05,
  };
  const noDelaySpringTransition = {
    type: "spring",
    stiffness,
    damping,
  };
  const slowSuccessTransition = {
    type: "spring",
    stiffness: 300,
    damping: 30,
    delay: index * 0.06,
  };

  useEffect(() => {
    animationControls.start({
      opacity: 1,
      y: 0,
      transition: springTransition,
    });
    return () => animationControls.stop();
  }, []);

  useEffect(() => {
    if (state === "success") {
      const transitionX = index * 72; // [Pharmino] 56px boxes + 16px gap
      animationControls.start({
        x: -transitionX,
        transition: slowSuccessTransition,
      });
    }
  }, [state, index, animationControls]);

  const onFocus = () => {
    animationControls.start({ y: -5, transition: noDelaySpringTransition });
  };

  const onBlur = () => {
    animationControls.start({ y: 0, transition: noDelaySpringTransition });
  };

  const onKeyDown = (e) => {
    const { value } = e.target;
    if (e.key === "Backspace" && !value && index > 0) {
      document.getElementById(`input-${index - 1}`)?.focus();
    } else if (e.key === "ArrowLeft" && index > 0) {
      document.getElementById(`input-${index - 1}`)?.focus();
    } else if (e.key === "ArrowRight" && index < 3) {
      // 4 inputs, so max index is 3
      document.getElementById(`input-${index + 1}`)?.focus();
    }
  };

  const onInput = (e) => {
    const { value } = e.target;
    if (value.match(/^[0-9]$/)) {
      // Only allow a single digit
      e.target.value = value;
      if (index < 3) {
        // 4 inputs, so move focus until the 3rd index
        document.getElementById(`input-${index + 1}`)?.focus();
      }
    } else {
      e.target.value = "";
    }
    verifyOTP();
  };

  const onPaste = (e) => {
    e.preventDefault();
    const pastedData = e.clipboardData.getData("text").trim().slice(0, 4);
    const digits = pastedData.split("").filter((char) => /^[0-9]$/.test(char));

    digits.forEach((digit, i) => {
      const targetIndex = index + i;
      if (targetIndex < 4) {
        const input = document.getElementById(`input-${targetIndex}`) as HTMLInputElement | null;
        if (input) {
          input.value = digit;
        }
      }
    });

    const nextFocusIndex = Math.min(index + digits.length, 3);
    document.getElementById(`input-${nextFocusIndex}`)?.focus();

    // Use a timeout to ensure all values are set before verifying
    setTimeout(verifyOTP, 0);
  };

  return (
    <motion.div
      className={`w-14 h-14 rounded-2xl border shadow-lg overflow-hidden transition-all duration-200 ${
        state === "error"
          ? "border-red-400/80 bg-red-500/15"
          : state === "success"
            ? "border-emerald-400/80 bg-emerald-500/15"
            : "border-white/55 bg-white/15 focus-within:border-white focus-within:bg-white/25 focus-within:ring-4 focus-within:ring-white/25"
      }`}
      initial={{ opacity, y }}
      animate={animationControls}
    >
      <input
        id={`input-${index}`}
        type="text"
        inputMode="numeric"
        maxLength={1}
        onInput={onInput}
        onKeyDown={onKeyDown}
        onPaste={onPaste}
        onFocus={onFocus}
        onBlur={onBlur}
        autoComplete={index === 0 ? "one-time-code" : "off"}
        aria-label={`Digit ${index + 1}`}
        className="w-full h-full text-center text-2xl font-semibold outline-none bg-transparent text-white caret-white"
        disabled={state === "success"}
      />
    </motion.div>
  );
};

// --- Main Verification Component ---

// [Pharmino] What the server answers when a code is checked.
export type OTPVerifyResult = boolean | { ok: boolean; message?: string };

export function OTPVerification({
  stiffness = 700,
  damping = 20,
  y = 10,
  opacity = 0,
  // [Pharmino] Optional wiring. Leave these out and the component behaves exactly like the original demo.
  email = "yourname@example.com",
  onVerify,
  onResend,
  onSuccess,
  onChangeEmail,
}: {
  stiffness?: number;
  damping?: number;
  y?: number;
  opacity?: number;
  /** Shown under the title instead of the placeholder address. */
  email?: string;
  /** Checks a complete code (e.g. with the server). Replaces the demo's hard-coded "1234". */
  onVerify?: (code: string) => Promise<OTPVerifyResult>;
  /** Sends a new code when "Click to resend" is pressed. */
  onResend?: () => void | Promise<void>;
  /** Called once the success animation has had time to play. */
  onSuccess?: () => void;
  /** [Pharmino] Goes back to the email step when the address was mistyped. */
  onChangeEmail?: () => void;
}) {
  const [state, setState] = useState("idle");
  const [countdown, setCountdown] = useState(60);
  const [isResendDisabled, setIsResendDisabled] = useState(true);
  const animationControls = useAnimationControls();
  // [Pharmino] Server message for the error line, and a guard so one code is only checked once at a time.
  const [errorMessage, setErrorMessage] = useState<string | undefined>(undefined);
  const checking = useRef(false);
  const verified = useRef(false);

  useEffect(() => {
    let timer;
    if (isResendDisabled) {
      timer = setInterval(() => {
        setCountdown((prevCountdown) => {
          if (prevCountdown <= 1) {
            clearInterval(timer);
            setIsResendDisabled(false);
            return 0;
          }
          return prevCountdown - 1;
        });
      }, 1000);
    }
    return () => clearInterval(timer);
  }, [isResendDisabled]);

  const getCode = () => {
    let code = "";
    for (let i = 0; i < 4; i++) {
      const input = document.getElementById(`input-${i}`) as HTMLInputElement | null;
      if (input) code += input.value;
    }
    return code;
  };

  const verifyOTP = () => {
    const code = getCode();
    if (code.length < 4) {
      setState("idle");
      return null;
    }

    // [Pharmino] Real check: ask the server instead of comparing with "1234".
    if (onVerify) {
      if (checking.current || verified.current) return null;
      checking.current = true;
      onVerify(code)
        .then((result) => {
          const ok = typeof result === "boolean" ? result : result.ok;
          if (ok) {
            verified.current = true;
            setState("success");
            setTimeout(() => onSuccess?.(), 1100);
          } else {
            rejected(typeof result === "boolean" ? undefined : result.message);
          }
        })
        .catch(() => rejected("Couldn't check the code. Please try again."))
        .finally(() => {
          checking.current = false;
        });
      return null;
    }

    console.log("Verifying code:", code);
    // This is where you would add your verification logic
    if (code === "1234") {
      // Mock success
      setState("success");
      return true;
    } else {
      // Mock error
      errorAnimation();
      return false;
    }
  };

  const errorAnimation = async () => {
    setState("error");
    await animationControls.start({
      x: [0, 5, -5, 5, -5, 0],
      transition: { duration: 0.3 },
    });
    // Reset state after animation to allow re-entry
    setTimeout(() => {
      if (getCode().length < 4) setState("idle");
    }, 500);
  };

  // [Pharmino] Every server check uses up one of a few attempts, so after a wrong code the boxes are
  // emptied: otherwise typing over the old digits would send a new "complete" code on each keystroke.
  // The error line stays until the next digit is typed.
  const rejected = async (message?: string) => {
    setErrorMessage(message);
    setState("error");
    await animationControls.start({
      x: [0, 5, -5, 5, -5, 0],
      transition: { duration: 0.3 },
    });
    for (let i = 0; i < 4; i++) {
      const input = document.getElementById(`input-${i}`) as HTMLInputElement | null;
      if (input) input.value = "";
    }
    document.getElementById("input-0")?.focus();
  };

  const handleResend = () => {
    console.log("Resending code...");
    setCountdown(60);
    setIsResendDisabled(true);
    // Add your actual resend logic here
    // [Pharmino] Real resend.
    setErrorMessage(undefined);
    void onResend?.();
  };

  return (
    // [Pharmino] Look of 21st.dev "OTP Verify" by minhxthanh: tunnel animation under a blue gradient.
    <div className="relative w-full max-w-sm overflow-hidden rounded-3xl shadow-2xl">
      <div className="absolute inset-0 z-0" aria-hidden="true">
        <img
          src="https://cdn.21st.dev/assets/mirror/44/447cf94cf584093396582baae6030e44c02a7c06df1fa6b931aeedbf97afa518.gif"
          alt=""
          className="w-full h-full object-cover opacity-60"
        />
        <div className="absolute inset-0 bg-gradient-to-b from-blue-600/85 via-blue-800/90 to-[#0a1033]" />
      </div>

      <div className="relative z-10 px-6 py-12 sm:px-8 sm:py-14">
        <div className="text-center mb-8">
          <div className="w-12 h-12 mx-auto mb-6 rounded-2xl bg-white/15 border border-white/35 text-white flex items-center justify-center shadow-lg">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" className="w-6 h-6" aria-hidden="true">
              <rect x="3" y="5" width="18" height="14" rx="2" />
              <path d="m3 7 9 6 9-6" />
            </svg>
          </div>
          <h1 className="text-2xl font-semibold text-white mb-3">
            {state === "success" ? "You're verified" : "Enter verification code"}
          </h1>
          {state !== "success" && (
            <p className="text-white/85 text-sm leading-relaxed">
              We emailed a 4-digit code to
              <br />
              <span className="text-white font-medium break-all">{email}</span>
            </p>
          )}
        </div>

        <AnimatePresence mode="wait">
          {state === "success" ? (
            <motion.div
              key="success"
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -20 }}
              transition={{ duration: 0.3 }}
              className="flex items-center justify-center"
              style={{ height: "168px" }}
            >
              <OTPSuccess />
            </motion.div>
          ) : (
            <motion.div
              key="form"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              transition={{ duration: 0.2 }}
            >
              <div className="flex flex-col items-center justify-center mb-10 relative h-16" dir="ltr">
                <motion.div animate={animationControls} className="flex items-center justify-center gap-4">
                  {Array.from({ length: 4 }).map((_, index) => (
                    <OTPInputBox
                      key={`input-${index}`}
                      index={index}
                      verifyOTP={verifyOTP}
                      state={state}
                      stiffness={stiffness}
                      damping={damping}
                      y={y}
                      opacity={opacity}
                    />
                  ))}
                </motion.div>
                <AnimatePresence>
                  {state === "error" && <OTPError message={errorMessage} />}
                </AnimatePresence>
              </div>

              <div className="text-center mb-6 text-sm">
                <span className="text-white/85">Didn't get the code? </span>
                {isResendDisabled ? (
                  <span className="text-white/75 tabular-nums">Resend in {countdown}s</span>
                ) : (
                  <button
                    type="button"
                    onClick={handleResend}
                    className="text-white font-semibold underline underline-offset-4 decoration-white/50 hover:decoration-white transition-colors duration-200 focus:outline-none focus-visible:decoration-2"
                  >
                    Resend
                  </button>
                )}
              </div>

              <div className="text-center">
                <p className="text-white/80 text-[13px] leading-relaxed">
                  Not in your inbox? Check your spam folder.
                  {onChangeEmail && (
                    <>
                      <br />
                      Typo in your address?{" "}
                      <button
                        type="button"
                        onClick={onChangeEmail}
                        className="text-white font-semibold underline underline-offset-4 decoration-white/50 hover:decoration-white transition-colors"
                      >
                        Edit email
                      </button>
                    </>
                  )}
                </p>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </div>
  );
}

export default OTPVerification;
