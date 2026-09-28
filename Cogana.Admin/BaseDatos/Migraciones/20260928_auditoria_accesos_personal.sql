create table if not exists public.staff_access_audit (
  id uuid primary key default gen_random_uuid(),
  store_id uuid not null references public.stores(id) on delete cascade,
  actor_user_id uuid not null references auth.users(id) on delete restrict,
  target_user_id uuid not null references auth.users(id) on delete restrict,
  action text not null check (action in ('invite', 'membership_created', 'access_updated')),
  previous_role text check (previous_role is null or previous_role in ('owner', 'admin', 'driver')),
  new_role text check (new_role is null or new_role in ('owner', 'admin', 'driver')),
  previous_active boolean,
  new_active boolean,
  created_at timestamptz not null default now()
);

create index if not exists staff_access_audit_store_created_idx
  on public.staff_access_audit(store_id, created_at desc);
create index if not exists staff_access_audit_actor_user_idx
  on public.staff_access_audit(actor_user_id);
create index if not exists staff_access_audit_target_user_idx
  on public.staff_access_audit(target_user_id);

alter table public.staff_access_audit enable row level security;
revoke all on public.staff_access_audit from public, anon, authenticated;
grant select on public.staff_access_audit to authenticated;
grant select, insert on public.staff_access_audit to service_role;

drop policy if exists staff_access_audit_owner_select on public.staff_access_audit;
create policy staff_access_audit_owner_select
  on public.staff_access_audit
  for select
  to authenticated
  using ((select private.has_store_role(store_id, array['owner'])));
