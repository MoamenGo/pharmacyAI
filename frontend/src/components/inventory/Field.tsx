import { forwardRef } from "react";

type Props = React.InputHTMLAttributes<HTMLInputElement> & { label: string };

export const Field = forwardRef<HTMLInputElement, Props>(function Field({ label, id, className, ...input }, ref) {
  return (
    <label htmlFor={id} className={`block ${className ?? ""}`}>
      <span className="mb-1 block text-sm text-slate-400">{label}</span>
      <input
        id={id}
        ref={ref}
        autoComplete="off"
        className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
        {...input}
      />
    </label>
  );
});
