import { useId } from "react";
import { ChevronDownIcon, ResetIcon } from "@radix-ui/react-icons";

export function Button({
  children,
  primary = false,
  className = "",
  ...props
}) {
  return (
    <button
      type="button"
      className={`${primary ? "button-primary" : "button"} ${className}`}
      {...props}
    >
      {children}
    </button>
  );
}

export function Switch({ label, checked, onChange, disabled }) {
  return (
    <button
      type="button"
      role="switch"
      aria-label={label}
      aria-checked={Boolean(checked)}
      disabled={disabled}
      className="switch"
      onClick={() => onChange(!checked)}
    >
      <span />
    </button>
  );
}

export function Disclosure({
  title,
  children,
  open,
  onChange,
  tools,
  description,
  status,
}) {
  const id = useId();
  return (
    <section className="disclosure">
      <div className="flex min-w-0 items-center gap-3 p-4">
        <button
          type="button"
          aria-expanded={open}
          aria-controls={id}
          onClick={() => onChange(!open)}
          className="flex min-w-0 flex-1 items-center gap-3 text-left"
        >
          <ChevronDownIcon
            className={`h-5 w-5 shrink-0 transition-transform duration-150 ${open ? "rotate-180" : ""}`}
          />
          <span className="min-w-0 flex-1">
            <span className="block font-semibold">{title}</span>
            {description && (
              <span className="mt-1 block text-sm text-muted">
                {description}
              </span>
            )}
          </span>
          {status && (
            <span className="badge hidden sm:inline-flex">{status}</span>
          )}
        </button>
        <div className="flex shrink-0 items-center gap-2">{tools}</div>
      </div>
      {open && (
        <div id={id} className="border-t border-line px-4 pb-4 pt-3">
          {children}
        </div>
      )}
    </section>
  );
}

export function Setting({
  label,
  kind = "text",
  value,
  onChange,
  low,
  high,
  description,
  choices,
  fallback,
  disabled,
  override,
}) {
  const id = useId();
  const options = choices?.map(String);
  const boolean = kind === "bool" && !options?.length;
  const numeric = ["float", "int"].includes(kind) && !options?.length;
  const hint =
    fallback === undefined
      ? null
      : `Default: ${fallback === "" ? "empty" : fallback}${low != null && high != null ? ` · ${low}–${high}` : ""}`;
  const update = (event) =>
    onChange(
      boolean
        ? event.target.checked
        : numeric && event.target.value !== ""
          ? Number(event.target.value)
          : event.target.value,
    );
  return (
    <div className="min-w-0 space-y-2">
      <div className="flex items-center justify-between gap-2">
        <label htmlFor={id} className="text-sm font-medium">
          {label}
        </label>
        {override && <Switch label={"Override " + label} {...override} />}
      </div>
      {description && (
        <p className="text-xs leading-relaxed text-muted">{description}</p>
      )}
      <div className="flex items-center gap-2">
        {options?.length ? (
          <select
            id={id}
            value={value ?? ""}
            disabled={disabled}
            onChange={update}
            className="field flex-1"
          >
            {!options.includes(String(value)) && (
              <option value={value}>Saved custom: {value}</option>
            )}
            {options.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        ) : boolean ? (
          <input
            id={id}
            type="checkbox"
            className="checkbox"
            checked={Boolean(value)}
            disabled={disabled}
            onChange={update}
          />
        ) : (
          <input
            id={id}
            type={numeric ? "number" : "text"}
            min={low ?? undefined}
            max={high ?? undefined}
            step={kind === "int" ? 1 : "any"}
            className="field flex-1"
            value={value ?? ""}
            disabled={disabled}
            onChange={update}
          />
        )}
        {fallback !== undefined && (
          <Button
            title={hint}
            aria-label={"Reset " + label + " to default"}
            disabled={disabled}
            onClick={() => onChange(fallback)}
            className="px-2"
          >
            <ResetIcon />
          </Button>
        )}
      </div>
      {numeric && low != null && high != null && high - low <= 10000 && (
        <input
          aria-label={label + " slider"}
          type="range"
          className="range w-full"
          min={low}
          max={high}
          step={kind === "int" ? 1 : Math.max(0.001, (high - low) / 1000)}
          value={value ?? low}
          disabled={disabled}
          onChange={(event) => onChange(Number(event.target.value))}
        />
      )}
      {hint && <p className="text-xs text-muted">{hint}</p>}
    </div>
  );
}

export function Empty({ title, children }) {
  return (
    <div className="rounded-lg border border-dashed border-line p-8 text-center">
      <h3 className="font-medium">{title}</h3>
      <p className="mt-2 text-sm text-muted">{children}</p>
    </div>
  );
}
