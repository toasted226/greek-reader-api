create table users (
    id bigint generated always as identity primary key,
    email text not null,
    created_at timestamptz not null default now()
);

create unique index users_email_lower_idx on users(lower(email));

create table login_tokens (
    id bigint generated always as identity primary key,
    user_id bigint not null references users(id) on delete cascade,
    token_hash bytea not null unique,
    expires_at timestamptz not null,
    used_at timestamptz
);

create unique index login_tokens_user_id_idx on login_tokens(user_id);