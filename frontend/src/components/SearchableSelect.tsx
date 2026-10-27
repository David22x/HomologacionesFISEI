import { useEffect, useState } from 'react';

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
  const [query, setQuery] = useState(selected?.label ?? '');

  useEffect(() => {
    if (selected) setQuery(selected.label);
  }, [value, selected?.label]);

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
          setQuery(nextQuery);
          const match = options.find(
            (option) => option.label.toLocaleLowerCase() === nextQuery.toLocaleLowerCase(),
          );
          if (match) onChange(match.value);
          else if (!nextQuery) onChange('');
        }}
      />
      <datalist id={listId}>
        {options.map((option) => <option key={option.value} value={option.label} />)}
      </datalist>
    </div>
  );
}
