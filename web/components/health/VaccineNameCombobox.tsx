"use client";
import { useId, useMemo, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { cn } from "../../lib/cn";
import type { CustomVaccineEntryResponse, VaccineCategory, VaccineTypeResponse } from "../../lib/types";

interface VaccineNameComboboxOption {
  key: string;
  name: string;
  vaccineTypeId: string | null;
  group: VaccineCategory | "custom";
}

interface VaccineNameComboboxProps {
  id?: string;
  value: string;
  vaccineTypes: VaccineTypeResponse[];
  customEntries: CustomVaccineEntryResponse[];
  onChange: (text: string) => void;
  onSelect: (option: { name: string; vaccineTypeId: string | null }) => void;
}

/**
 * Vaccine-name picker (spec.md FR-013, research.md R5) — a first-party combobox, not a new UI
 * dependency (no cmdk/@radix-ui/react-popover exists in this codebase yet, and one picker
 * doesn't justify adding one). Typing never clears a prior selection (spec.md FR-004) — only
 * picking a new option replaces it; the parent form owns that state, this component only reports
 * text changes and explicit selections separately.
 *
 * `id` is meant to be paired with an external `<label htmlFor={id}>` rather than a wrapping
 * `<label>` — the dropdown listbox must never be a DOM descendant of the label, or its option
 * text gets folded into the label's accessible name the moment it opens.
 */
export function VaccineNameCombobox({ id, value, vaccineTypes, customEntries, onChange, onSelect }: VaccineNameComboboxProps) {
  const t = useTranslations("children.health.vaccines.combobox");
  const listboxId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(0);

  const options = useMemo<VaccineNameComboboxOption[]>(() => {
    const query = value.trim().toLowerCase();
    const catalogOptions = vaccineTypes
      .filter((v) => v.name.toLowerCase().includes(query))
      .map((v) => ({ key: `catalog:${v.id}`, name: v.name, vaccineTypeId: v.id, group: v.category ?? ("aanbevolen_niet_gratis" as VaccineCategory) }));
    const customOptions = customEntries
      .filter((e) => e.name.toLowerCase().includes(query))
      .map((e) => ({ key: `custom:${e.id}`, name: e.name, vaccineTypeId: null, group: "custom" as const }));
    return [...catalogOptions, ...customOptions];
  }, [value, vaccineTypes, customEntries]);

  const groups = useMemo(() => {
    const basis = options.filter((o) => o.group === "basisvaccinatieschema");
    const recommended = options.filter((o) => o.group === "aanbevolen_niet_gratis");
    const custom = options.filter((o) => o.group === "custom");
    return [
      { label: t("catalogCategory.basisvaccinatieschema"), options: basis },
      { label: t("catalogCategory.aanbevolen_niet_gratis"), options: recommended },
      { label: t("usedBeforeGroup"), options: custom },
    ].filter((g) => g.options.length > 0);
  }, [options, t]);

  const flatOptions = groups.flatMap((g) => g.options);

  function selectOption(option: VaccineNameComboboxOption) {
    onSelect({ name: option.name, vaccineTypeId: option.vaccineTypeId });
    setOpen(false);
    inputRef.current?.focus();
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setOpen(true);
      setHighlightedIndex((i) => Math.min(i + 1, flatOptions.length - 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setHighlightedIndex((i) => Math.max(i - 1, 0));
    } else if (e.key === "Enter") {
      if (open && flatOptions[highlightedIndex]) {
        e.preventDefault();
        selectOption(flatOptions[highlightedIndex]);
      }
    } else if (e.key === "Escape") {
      setOpen(false);
    }
  }

  const activeOptionId = open && flatOptions[highlightedIndex] ? `${listboxId}-${flatOptions[highlightedIndex].key}` : undefined;

  return (
    <div className="relative">
      <input
        ref={inputRef}
        id={id}
        role="combobox"
        aria-expanded={open}
        aria-controls={listboxId}
        aria-autocomplete="list"
        aria-activedescendant={activeOptionId}
        className="mt-2 flex h-10 w-full rounded-lg bg-surface-soft px-3 py-2 text-sm text-text placeholder:text-placeholder focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark dark:placeholder:text-placeholder-dark"
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
          setOpen(true);
          setHighlightedIndex(0);
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => setTimeout(() => setOpen(false), 150)}
        onKeyDown={handleKeyDown}
      />
      {open && (
        <ul id={listboxId} role="listbox" className="absolute z-10 mt-1 max-h-64 w-full overflow-auto rounded-lg border border-border bg-surface shadow-lg dark:border-border-dark dark:bg-surface-dark">
          {flatOptions.length === 0 && (
            <li className="px-3 py-2 text-sm text-text-soft dark:text-text-soft-dark">{t("addCustom", { name: value })}</li>
          )}
          {groups.map((group) => (
            <li key={group.label}>
              <div className="px-3 pt-2 text-xs font-medium uppercase text-text-soft dark:text-text-soft-dark">{group.label}</div>
              <ul>
                {group.options.map((option) => {
                  const flatIndex = flatOptions.indexOf(option);
                  return (
                    <li
                      key={option.key}
                      id={`${listboxId}-${option.key}`}
                      role="option"
                      aria-selected={flatIndex === highlightedIndex}
                      className={cn(
                        "cursor-pointer px-3 py-2 text-sm text-text dark:text-text-dark",
                        flatIndex === highlightedIndex && "bg-primary-soft dark:bg-primary-soft-dark",
                      )}
                      onMouseDown={(e) => e.preventDefault()}
                      onClick={() => selectOption(option)}
                    >
                      {option.name}
                    </li>
                  );
                })}
              </ul>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
