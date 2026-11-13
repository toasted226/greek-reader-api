-- Lexicon browsing orders by lemma with the "C" collation so page boundaries
-- match the ordinal sort used in C#. This index keeps unfiltered pages cheap.
create index lemmas_lemma_c_idx on lemmas (lemma collate "C", id);
