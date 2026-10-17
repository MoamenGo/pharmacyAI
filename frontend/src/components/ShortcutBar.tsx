export interface Shortcut {
  key: string;
  label: string;
}

export function ShortcutBar({ shortcuts }: { shortcuts: Shortcut[] }) {
  return (
    <footer className="fixed inset-x-0 bottom-0 flex gap-2 border-t border-slate-800 bg-slate-900 px-4 py-2 text-sm">
      {shortcuts.map((s) => (
        <span key={s.key} className="flex items-center gap-1.5 text-slate-300">
          <kbd className="rounded border border-slate-600 bg-slate-800 px-1.5 py-0.5 font-mono text-xs" dir="ltr">
            {s.key}
          </kbd>
          {s.label}
        </span>
      ))}
    </footer>
  );
}
