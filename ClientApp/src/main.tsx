/*
 * Entry point of the sign-up bundle (Web/wwwroot/dist/signup/signup.js), loaded by Views/Account/Register.cshtml.
 * The Razor page stays plain HTML + JS; this file gives it two things on window.PharminoSignup:
 *   - mountOtp(): renders the OTPVerification React component into an element (step 6)
 *   - phone: libphonenumber-js helpers for the phone step (all countries, formatting, validation)
 */
import "./index.css";
import { createRoot, type Root } from "react-dom/client";
import { OTPVerification, type OTPVerifyResult } from "@/components/ui/otp-input";
import { AsYouType, getCountries, getCountryCallingCode, isValidPhoneNumber, parsePhoneNumber } from "libphonenumber-js/min";

type MountOptions = {
  email: string;
  onVerify: (code: string) => Promise<OTPVerifyResult>;
  onResend: () => void | Promise<void>;
  onSuccess: () => void;
};

const settings = { stiffness: 700, damping: 20, y: 10, opacity: 0 };

function mountOtp(el: HTMLElement, options: MountOptions): { unmount: () => void } {
  const root: Root = createRoot(el);
  root.render(<OTPVerification {...settings} {...options} />);
  return { unmount: () => root.unmount() };
}

const phone = { AsYouType, getCountries, getCountryCallingCode, isValidPhoneNumber, parsePhoneNumber };

declare global {
  interface Window {
    PharminoSignup: { mountOtp: typeof mountOtp; phone: typeof phone };
  }
}

window.PharminoSignup = { mountOtp, phone };
window.dispatchEvent(new Event("pharmino-signup-ready"));
