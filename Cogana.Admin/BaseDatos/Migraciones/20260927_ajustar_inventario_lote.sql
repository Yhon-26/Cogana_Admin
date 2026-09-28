create or replace function private.adjust_inventory_lot(
  p_store_id uuid,
  p_lot_id uuid,
  p_counted_quantity bigint,
  p_operation_id uuid,
  p_reason text
)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $function$
declare
  v_actor uuid := (select auth.uid());
  v_existing private.admin_operation_results%rowtype;
  v_lot public.inventory_lots%rowtype;
  v_delta bigint;
  v_new_status text;
  v_result jsonb;
begin
  if v_actor is null or not private.has_store_role(p_store_id, array['owner','admin']) then
    raise exception using errcode = '42501', message = 'No autorizado para ajustar inventario';
  end if;

  if p_operation_id is null then
    raise exception using errcode = '22023', message = 'operation_id es obligatorio';
  end if;

  if p_counted_quantity < 0 then
    raise exception using errcode = '22023', message = 'La cantidad contada no puede ser negativa';
  end if;

  if pg_catalog.btrim(coalesce(p_reason, '')) = '' then
    raise exception using errcode = '22023', message = 'El motivo del ajuste es obligatorio';
  end if;

  perform pg_catalog.pg_advisory_xact_lock(
    pg_catalog.hashtextextended(p_operation_id::text, 0)
  );

  select * into v_existing
  from private.admin_operation_results
  where store_id = p_store_id and operation_id = p_operation_id;

  if found then
    if v_existing.operation_type <> 'adjust_inventory_lot' then
      raise exception using errcode = '23505', message = 'operation_id ya utilizado';
    end if;

    return v_existing.result || pg_catalog.jsonb_build_object('idempotent_replay', true);
  end if;

  select * into v_lot
  from public.inventory_lots
  where store_id = p_store_id and id = p_lot_id
  for update;

  if not found then
    raise exception using errcode = '22023', message = 'Lote no válido para la tienda';
  end if;

  v_delta := p_counted_quantity - v_lot.loose_on_hand_quantity;
  if v_delta = 0 then
    raise exception using errcode = '22023', message = 'La cantidad contada es igual al stock actual';
  end if;

  v_new_status := case
    when p_counted_quantity = 0 then 'depleted'
    when v_lot.status = 'depleted' then 'available'
    else v_lot.status
  end;

  update public.inventory_lots
  set loose_on_hand_quantity = p_counted_quantity,
      status = v_new_status,
      updated_at = pg_catalog.statement_timestamp()
  where store_id = p_store_id and id = p_lot_id;

  insert into public.inventory_movements (
    store_id,
    product_id,
    lot_id,
    movement_type,
    base_quantity_delta,
    loose_quantity_delta,
    sealed_units_delta,
    reason,
    operation_id,
    actor_user_id
  ) values (
    p_store_id,
    v_lot.product_id,
    p_lot_id,
    'adjustment',
    v_delta,
    v_delta,
    0,
    pg_catalog.btrim(p_reason),
    p_operation_id,
    v_actor
  );

  v_result := pg_catalog.jsonb_build_object(
    'lot_id', p_lot_id,
    'previous_quantity', v_lot.loose_on_hand_quantity,
    'counted_quantity', p_counted_quantity,
    'quantity_delta', v_delta,
    'status', v_new_status,
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
    'adjust_inventory_lot',
    v_actor,
    v_result
  );

  return v_result;
end;
$function$;

create or replace function public.adjust_inventory_lot(
  p_store_id uuid,
  p_lot_id uuid,
  p_counted_quantity bigint,
  p_operation_id uuid,
  p_reason text
)
returns jsonb
language sql
security invoker
set search_path = ''
as $function$
  select private.adjust_inventory_lot(
    p_store_id,
    p_lot_id,
    p_counted_quantity,
    p_operation_id,
    p_reason
  );
$function$;

revoke all on function public.adjust_inventory_lot(uuid,uuid,bigint,uuid,text) from public;
revoke all on function public.adjust_inventory_lot(uuid,uuid,bigint,uuid,text) from anon;
grant execute on function public.adjust_inventory_lot(uuid,uuid,bigint,uuid,text) to authenticated;
