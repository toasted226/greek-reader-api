-- Lexicon reads fetch examples for a page of lemma IDs. Without this index
-- each of those lookups scans the whole examples table.
create index examples_lemma_id_idx on examples (lemma_id);
