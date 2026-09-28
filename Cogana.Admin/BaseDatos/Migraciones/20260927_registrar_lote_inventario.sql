create table if not exists private.admin_operation_results (
  store_id uuid not null references public.stores(id) on delete cascade,
  operation_id uuid not null,
  operation_type text not null,
  actor_user_id uuid references auth.users(id) on delete set null,
  result jsonb not null,
  created_at timestamptz not null default now(),
  primary key (store_id, operation_id)
);

alter table private.admin_operation_results enable row level security;

create or replace function private.register_inventory_lot(
  p_store_id uuid,
  p_product_id uuid,
  p_supplier_id uuid,
  p_lot_code text,
  p_expires_on date,
  p_quantity bigint,
  p_operation_id uuid,
  p_notes text default null
)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $function$
declare
  v_actor uuid := (select auth.uid());
  v_existing private.admin_operation_results%rowtype;
  v_lot_id uuid;
  v_result jsonb;
begin
  if v_actor is null or not private.has_store_role(p_store_id, array['owner','admin']) then
    raise exception using errcode = '42501', message = 'No autorizado para registrar lotes';
  end if;

  if p_operation_id is null then
    raise exception using errcode = '22023', message = 'operation_id es obligatorio';
  end if;

  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(p_operation_id::text, 0));

  select * into v_existing
  from private.admin_operation_results
  where store_id = p_store_id and operation_id = p_operation_id;

  if found then
    if v_existing.operation_type <> 'register_inventory_lot' then
      raise exception using errcode = '23505', message = 'operation_id ya utilizado';
    end if;

    return v_existing.result || jsonb_build_object('idempotent_replay', true);
  end if;

  if p_product_id is null or not exists (
    select 1 from public.products
    where id = p_product_id and store_id = p_store_id
  ) then
    raise exception using errcode = '22023', message = 'Producto no válido para la tienda';
  end if;

  if p_supplier_id is not null and not exists (
    select 1 from public.suppliers
    where id = p_supplier_id and store_id = p_store_id and is_active
  ) then
    raise exception using errcode = '22023', message = 'Proveedor no válido para la tienda';
  end if;

  if pg_catalog.btrim(coalesce(p_lot_code, '')) = '' then
    raise exception using errcode = '22023', message = 'El código de lote es obligatorio';
  end if;

  if p_quantity <= 0 then
    raise exception using errcode = '22023', message = 'La cantidad debe ser mayor que cero';
  end if;

  if p_expires_on is not null and p_expires_on < current_date then
    raise exception using errcode = '22023', message = 'La fecha de vencimiento no puede estar en el pasado';
  end if;

  insert into public.inventory_lots (
    store_id,
    product_id,
    supplier_id,
    lot_code,
    expires_on,
    loose_on_hand_quantity,
    status,
    notes,
    created_by
  ) values (
    p_store_id,
    p_product_id,
    p_supplier_id,
    pg_catalog.btrim(p_lot_code),
    p_expires_on,
    p_quantity,
    'available',
    nullif(pg_catalog.btrim(coalesce(p_notes, '')), ''),
    v_actor
  )
  returning id into v_lot_id;

  v_result := jsonb_build_object(
    'lot_id', v_lot_id,
    'status', 'available',
    'idempotent_replay', false
  );

  insert into private.admin_operation_results (
    store_id,
    operation_id,
    operation_type,
    actor_user_id,
    result
  ) values (
    p_store_id,
    p_operation_id,
    'register_inventory_lot',
    v_actor,
    v_result
  );

  return v_result;
end;
$function$;

create or replace function public.register_inventory_lot(
  p_store_id uuid,
  p_product_id uuid,
  p_supplier_id uuid,
  p_lot_code text,
  p_expires_on date,
  p_quantity bigint,
  p_operation_id uuid,
  p_notes text default null
)
returns jsonb
language sql
security invoker
set search_path = ''
as $function$
  select private.register_inventory_lot(
    p_store_id,
    p_product_id,
    p_supplier_id,
    p_lot_code,
    p_expires_on,
    p_quantity,
    p_operation_id,
    p_notes
  );
$function$;

revoke all on function public.register_inventory_lot(uuid,uuid,uuid,text,date,bigint,uuid,text) from public;
revoke all on function public.register_inventory_lot(uuid,uuid,uuid,text,date,bigint,uuid,text) from anon;
grant execute on function public.register_inventory_lot(uuid,uuid,uuid,text,date,bigint,uuid,text) to authenticated;
