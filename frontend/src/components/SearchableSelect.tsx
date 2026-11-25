import { useState } from 'react';

interface SearchableSelectOption {
  value: string;
  label: string;
}

interface SearchableSelectProps {
  id: string;
  label: string;
  value: string;
  options: SearchableSelectOption[];
  placeholder: string;
  disabled?: boolean;
  onChange: (value: string) => void;
}

export default function SearchableSelect({
  id,
  label,
  value,
  options,
  placeholder,
  disabled = false,
  onChange,
}: SearchableSelectProps) {
  const listId = `${id}-options`;
  const selected = options.find((option) => option.value === value);
  const selectedLabel = selected?.label ?? '';
  const [draft, setDraft] = useState({ selection: value, label: selectedLabel, query: selectedLabel });
  const query = draft.selection === value && draft.label === selectedLabel ? draft.query : selectedLabel;

  return (
    <div className="catalog-field">
      <label htmlFor={id}>{label}</label>
      <input
        id={id}
        type="search"
        list={listId}
        value={query}
        placeholder={placeholder}
        disabled={disabled}
        autoComplete="off"
        onChange={(event) => {
          const nextQuery = event.target.value;
          const match = options.find(
            (option) => option.label.toLocaleLowerCase() === nextQuery.toLocaleLowerCase(),
          );
          const nextValue = match?.value ?? '';
          setDraft({ selection: nextValue, label: match?.label ?? '', query: nextQuery });
          if (nextValue !== value) onChange(nextValue);
        }}
      />
      <datalist id={listId}>
        {options.map((option) => <option key={option.value} value={option.label} />)}
      </datalist>
    </div>
  );
}
