create index if not exists orders_store_created_idx
  on public.orders (store_id, created_at desc);
