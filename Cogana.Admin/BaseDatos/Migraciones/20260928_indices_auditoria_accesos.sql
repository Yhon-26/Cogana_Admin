create index if not exists staff_access_audit_actor_user_idx
  on public.staff_access_audit(actor_user_id);

create index if not exists staff_access_audit_target_user_idx
  on public.staff_access_audit(target_user_id);
