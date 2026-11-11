alter table user_lemmas
    add column saved_at timestamptz not null default now();
