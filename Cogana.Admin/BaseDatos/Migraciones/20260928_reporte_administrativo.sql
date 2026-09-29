create or replace function private.get_admin_report(
  p_store_id uuid,
  p_start_date date,
  p_end_date date
)
returns jsonb
language plpgsql
stable
security definer
set search_path = ''
as $function$
declare
  v_actor uuid := (select auth.uid());
  v_timezone text;
  v_from timestamptz;
  v_to timestamptz;
  v_result jsonb;
begin
  if v_actor is null or not private.has_store_role(p_store_id, array['owner','admin']) then
    raise exception using errcode = '42501', message = 'No autorizado para consultar reportes';
  end if;
  if p_start_date is null or p_end_date is null or p_end_date < p_start_date then
    raise exception using errcode = '22023', message = 'El periodo del reporte no es válido';
  end if;
  if p_end_date - p_start_date > 366 then
    raise exception using errcode = '22023', message = 'El periodo no puede superar 367 días';
  end if;

  select s.timezone into v_timezone
  from public.stores s
  where s.id = p_store_id;
  if not found then
    raise exception using errcode = '22023', message = 'La tienda no existe';
  end if;

  v_from := p_start_date::timestamp at time zone v_timezone;
  v_to := (p_end_date + 1)::timestamp at time zone v_timezone;

  with
  orders_period as materialized (
    select o.*
    from public.orders o
    where o.store_id = p_store_id
      and o.created_at >= v_from
      and o.created_at < v_to
  ),
  completed_orders as materialized (
    select * from orders_period where status = 'completed'
  ),
  product_sales as (
    select
      oi.product_id,
      pg_catalog.max(oi.product_name_snapshot) as product_name,
      pg_catalog.sum(coalesce(oi.prepared_quantity, oi.estimated_quantity, oi.requested_quantity, 0))::bigint as quantity,
      pg_catalog.sum(coalesce(oi.final_line_cents, oi.estimated_line_cents, 0))::bigint as revenue_cents,
      pg_catalog.count(distinct oi.order_id)::integer as orders_count
    from public.order_items oi
    join completed_orders o on o.id = oi.order_id and o.store_id = oi.store_id
    group by oi.product_id
  ),
  loose_stock as (
    select il.product_id, pg_catalog.sum(il.loose_on_hand_quantity)::bigint as quantity
    from public.inventory_lots il
    where il.store_id = p_store_id and il.status = 'available'
    group by il.product_id
  ),
  sealed_stock as (
    select
      il.product_id,
      pg_catalog.sum(ip.sealed_on_hand_units * pp.base_quantity)::bigint as quantity
    from public.inventory_lot_packages ip
    join public.inventory_lots il
      on il.store_id = ip.store_id and il.id = ip.lot_id
    join public.product_presentations pp
      on pp.store_id = ip.store_id and pp.id = ip.presentation_id
    where ip.store_id = p_store_id and il.status = 'available'
    group by il.product_id
  ),
  inventory_stock as (
    select
      p.id,
      p.name,
      p.base_unit,
      p.minimum_stock_quantity,
      (coalesce(ls.quantity, 0) + coalesce(ss.quantity, 0))::bigint as current_quantity
    from public.products p
    left join loose_stock ls on ls.product_id = p.id
    left join sealed_stock ss on ss.product_id = p.id
    where p.store_id = p_store_id and p.is_active
  )
  select pg_catalog.jsonb_build_object(
    'start_date', p_start_date,
    'end_date', p_end_date,
    'summary', pg_catalog.jsonb_build_object(
      'orders_count', (select pg_catalog.count(*) from orders_period),
      'completed_count', (select pg_catalog.count(*) from completed_orders),
      'cancelled_count', (select pg_catalog.count(*) from orders_period where status = 'cancelled'),
      'active_count', (select pg_catalog.count(*) from orders_period where status not in ('completed','cancelled')),
      'sales_cents', (select coalesce(pg_catalog.sum(coalesce(final_total_cents, estimated_total_cents)), 0) from completed_orders),
      'average_ticket_cents', (
        select case when pg_catalog.count(*) = 0 then 0
          else pg_catalog.round(coalesce(pg_catalog.sum(coalesce(final_total_cents, estimated_total_cents)), 0)::numeric / pg_catalog.count(*))::bigint end
        from completed_orders
      ),
      'discount_cents', (select coalesce(pg_catalog.sum(coalesce(final_discount_cents, estimated_discount_cents, 0)), 0) from completed_orders),
      'active_products_count', (select pg_catalog.count(*) from public.products products_count where products_count.store_id = p_store_id and products_count.is_active),
      'low_stock_count', (select pg_catalog.count(*) from inventory_stock where current_quantity <= minimum_stock_quantity),
      'expiring_lots_count', (
        select pg_catalog.count(*) from public.inventory_lots expiring_count
        where expiring_count.store_id = p_store_id and expiring_count.status = 'available'
          and expiring_count.expires_on between p_end_date and p_end_date + 30
      ),
      'expired_lots_count', (
        select pg_catalog.count(*) from public.inventory_lots expired_count
        where expired_count.store_id = p_store_id and expired_count.status = 'available' and expired_count.expires_on < p_end_date
      )
    ),
    'sales_by_day', coalesce((
      select pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
        'date', daily.local_date,
        'orders_count', daily.orders_count,
        'sales_cents', daily.sales_cents
      ) order by daily.local_date)
      from (
        select
          (o.created_at at time zone v_timezone)::date as local_date,
          pg_catalog.count(*)::integer as orders_count,
          pg_catalog.sum(coalesce(o.final_total_cents, o.estimated_total_cents))::bigint as sales_cents
        from completed_orders o
        group by (o.created_at at time zone v_timezone)::date
      ) daily
    ), '[]'::jsonb),
    'order_statuses', coalesce((
      select pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
        'status', grouped.status,
        'orders_count', grouped.orders_count,
        'total_cents', grouped.total_cents
      ) order by grouped.orders_count desc, grouped.status)
      from (
        select
          o.status,
          pg_catalog.count(*)::integer as orders_count,
          pg_catalog.sum(coalesce(o.final_total_cents, o.estimated_total_cents))::bigint as total_cents
        from orders_period o
        group by o.status
      ) grouped
    ), '[]'::jsonb),
    'top_products', coalesce((
      select pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
        'product_id', ranked.product_id,
        'product_name', ranked.product_name,
        'quantity', ranked.quantity,
        'revenue_cents', ranked.revenue_cents,
        'orders_count', ranked.orders_count
      ) order by ranked.revenue_cents desc, ranked.product_name)
      from (select * from product_sales order by revenue_cents desc, product_name limit 20) ranked
    ), '[]'::jsonb),
    'payment_methods', coalesce((
      select pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
        'method', grouped.method,
        'orders_count', grouped.orders_count,
        'total_cents', grouped.total_cents
      ) order by grouped.total_cents desc, grouped.method)
      from (
        select
          coalesce(nullif(o.payment_method, ''), 'not_specified') as method,
          pg_catalog.count(*)::integer as orders_count,
          pg_catalog.sum(coalesce(o.final_total_cents, o.estimated_total_cents))::bigint as total_cents
        from completed_orders o
        group by coalesce(nullif(o.payment_method, ''), 'not_specified')
      ) grouped
    ), '[]'::jsonb),
    'low_stock', coalesce((
      select pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
        'product_id', limited.id,
        'product_name', limited.name,
        'base_unit', limited.base_unit,
        'current_quantity', limited.current_quantity,
        'minimum_quantity', limited.minimum_stock_quantity
      ) order by limited.current_quantity, limited.name)
      from (
        select * from inventory_stock
        where current_quantity <= minimum_stock_quantity
        order by current_quantity, name
        limit 50
      ) limited
    ), '[]'::jsonb),
    'expiring_lots', coalesce((
      select pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
        'lot_id', limited.id,
        'lot_code', limited.lot_code,
        'product_name', limited.product_name,
        'expires_on', limited.expires_on,
        'quantity', limited.loose_on_hand_quantity,
        'status', limited.expiration_status
      ) order by limited.expires_on, limited.product_name)
      from (
        select
          il.id,
          il.lot_code,
          p.name as product_name,
          il.expires_on,
          il.loose_on_hand_quantity,
          case when il.expires_on < p_end_date then 'expired' else 'expiring' end as expiration_status
        from public.inventory_lots il
        join public.products p on p.store_id = il.store_id and p.id = il.product_id
        where il.store_id = p_store_id and il.status = 'available'
          and il.expires_on is not null and il.expires_on <= p_end_date + 30
        order by il.expires_on, p.name
        limit 50
      ) limited
    ), '[]'::jsonb)
  ) into v_result;

  return v_result;
end;
$function$;

create or replace function public.get_admin_report(
  p_store_id uuid,
  p_start_date date,
  p_end_date date
)
returns jsonb
language sql
stable
security invoker
set search_path = ''
as $function$
  select private.get_admin_report(p_store_id, p_start_date, p_end_date);
$function$;

revoke all on function public.get_admin_report(uuid,date,date) from public;
revoke all on function public.get_admin_report(uuid,date,date) from anon;
grant execute on function public.get_admin_report(uuid,date,date) to authenticated;
