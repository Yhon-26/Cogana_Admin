create or replace function private.save_promotion_admin(
  p_store_id uuid,
  p_promotion_id uuid,
  p_name text,
  p_description text,
  p_promotion_type text,
  p_value bigint,
  p_minimum_quantity bigint,
  p_minimum_order_cents bigint,
  p_coupon_code text,
  p_starts_at timestamptz,
  p_ends_at timestamptz,
  p_usage_limit integer,
  p_per_customer_limit integer,
  p_priority integer,
  p_is_stackable boolean,
  p_product_ids uuid[],
  p_operation_id uuid
)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $function$
declare
  v_actor uuid := (select auth.uid());
  v_existing private.admin_operation_results%rowtype;
  v_product_count integer;
  v_result jsonb;
begin
  if v_actor is null or not private.has_store_role(p_store_id, array['owner','admin']) then
    raise exception using errcode = '42501', message = 'No autorizado para administrar promociones';
  end if;

  if p_operation_id is null then
    raise exception using errcode = '22023', message = 'operation_id es obligatorio';
  end if;

  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(p_operation_id::text, 0));

  select * into v_existing
  from private.admin_operation_results
  where store_id = p_store_id and operation_id = p_operation_id;

  if found then
    if v_existing.operation_type <> 'save_promotion_admin' then
      raise exception using errcode = '23505', message = 'operation_id ya utilizado';
    end if;
    return v_existing.result || pg_catalog.jsonb_build_object('idempotent_replay', true);
  end if;

  perform 1
  from public.promotions
  where store_id = p_store_id and id = p_promotion_id
  for update;
  if not found then
    raise exception using errcode = '22023', message = 'Promoción no válida para la tienda';
  end if;

  if pg_catalog.btrim(coalesce(p_name, '')) = '' then
    raise exception using errcode = '22023', message = 'El nombre es obligatorio';
  end if;
  if p_promotion_type not in ('percentage','fixed_amount','fixed_price','quantity_price','free_delivery') then
    raise exception using errcode = '22023', message = 'Tipo de promoción no válido';
  end if;
  if p_starts_at is null or p_ends_at is null or p_ends_at <= p_starts_at then
    raise exception using errcode = '22023', message = 'El periodo de vigencia no es válido';
  end if;
  if p_value < 0 or (p_promotion_type = 'percentage' and (p_value < 1 or p_value > 100)) then
    raise exception using errcode = '22023', message = 'El valor de la promoción no es válido';
  end if;
  if p_promotion_type in ('fixed_amount','fixed_price','quantity_price') and p_value <= 0 then
    raise exception using errcode = '22023', message = 'El importe debe ser mayor que cero';
  end if;
  if p_promotion_type = 'quantity_price' and coalesce(p_minimum_quantity, 0) <= 0 then
    raise exception using errcode = '22023', message = 'La cantidad mínima es obligatoria';
  end if;
  if coalesce(p_minimum_order_cents, 0) < 0 or coalesce(p_priority, 0) < 0 then
    raise exception using errcode = '22023', message = 'Los valores mínimos no pueden ser negativos';
  end if;
  if p_usage_limit is not null and p_usage_limit <= 0 then
    raise exception using errcode = '22023', message = 'El límite de usos debe ser mayor que cero';
  end if;
  if p_per_customer_limit is not null and p_per_customer_limit <= 0 then
    raise exception using errcode = '22023', message = 'El límite por cliente debe ser mayor que cero';
  end if;

  if exists (
    select 1
    from pg_catalog.unnest(coalesce(p_product_ids, array[]::uuid[])) as selected(product_id)
    where not exists (
      select 1 from public.products p
      where p.store_id = p_store_id and p.id = selected.product_id
    )
  ) then
    raise exception using errcode = '22023', message = 'Uno de los productos no pertenece a la tienda';
  end if;

  update public.promotions
  set name = pg_catalog.btrim(p_name),
      description = nullif(pg_catalog.btrim(coalesce(p_description, '')), ''),
      promotion_type = p_promotion_type,
      value = case when p_promotion_type = 'free_delivery' then 0 else p_value end,
      minimum_quantity = case when p_promotion_type = 'quantity_price' then p_minimum_quantity else null end,
      minimum_order_cents = coalesce(p_minimum_order_cents, 0),
      coupon_code = nullif(pg_catalog.upper(pg_catalog.btrim(coalesce(p_coupon_code, ''))), ''),
      starts_at = p_starts_at,
      ends_at = p_ends_at,
      usage_limit = p_usage_limit,
      per_customer_limit = p_per_customer_limit,
      priority = coalesce(p_priority, 0),
      is_stackable = coalesce(p_is_stackable, false),
      updated_at = pg_catalog.statement_timestamp()
  where store_id = p_store_id and id = p_promotion_id;

  delete from public.promotion_products
  where store_id = p_store_id and promotion_id = p_promotion_id;

  insert into public.promotion_products (store_id, promotion_id, product_id, presentation_id)
  select p_store_id, p_promotion_id, selected.product_id, null
  from (
    select distinct product_id
    from pg_catalog.unnest(coalesce(p_product_ids, array[]::uuid[])) as products(product_id)
  ) selected;

  select pg_catalog.count(*)::integer into v_product_count
  from public.promotion_products
  where store_id = p_store_id and promotion_id = p_promotion_id;

  v_result := pg_catalog.jsonb_build_object(
    'promotion_id', p_promotion_id,
    'product_count', v_product_count,
    'idempotent_replay', false
  );

  insert into private.admin_operation_results (
    store_id, operation_id, operation_type, actor_user_id, result
  ) values (
    p_store_id, p_operation_id, 'save_promotion_admin', v_actor, v_result
  );

  return v_result;
end;
$function$;

create or replace function public.save_promotion_admin(
  p_store_id uuid,
  p_promotion_id uuid,
  p_name text,
  p_description text,
  p_promotion_type text,
  p_value bigint,
  p_minimum_quantity bigint,
  p_minimum_order_cents bigint,
  p_coupon_code text,
  p_starts_at timestamptz,
  p_ends_at timestamptz,
  p_usage_limit integer,
  p_per_customer_limit integer,
  p_priority integer,
  p_is_stackable boolean,
  p_product_ids uuid[],
  p_operation_id uuid
)
returns jsonb
language sql
security invoker
set search_path = ''
as $function$
  select private.save_promotion_admin(
    p_store_id, p_promotion_id, p_name, p_description, p_promotion_type,
    p_value, p_minimum_quantity, p_minimum_order_cents, p_coupon_code,
    p_starts_at, p_ends_at, p_usage_limit, p_per_customer_limit,
    p_priority, p_is_stackable, p_product_ids, p_operation_id
  );
$function$;

revoke all on function public.save_promotion_admin(uuid,uuid,text,text,text,bigint,bigint,bigint,text,timestamptz,timestamptz,integer,integer,integer,boolean,uuid[],uuid) from public;
revoke all on function public.save_promotion_admin(uuid,uuid,text,text,text,bigint,bigint,bigint,text,timestamptz,timestamptz,integer,integer,integer,boolean,uuid[],uuid) from anon;
grant execute on function public.save_promotion_admin(uuid,uuid,text,text,text,bigint,bigint,bigint,text,timestamptz,timestamptz,integer,integer,integer,boolean,uuid[],uuid) to authenticated;
