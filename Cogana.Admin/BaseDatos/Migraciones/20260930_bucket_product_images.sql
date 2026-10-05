-- Buckets y politicas de archivos para Cogana v2.

begin;

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values
  (
    'product-images',
    'product-images',
    true,
    5242880,
    array['image/jpeg', 'image/png', 'image/webp', 'image/avif']
  ),
  (
    'payment-proofs',
    'payment-proofs',
    false,
    10485760,
    array['image/jpeg', 'image/png', 'image/webp', 'application/pdf']
  ),
  (
    'delivery-evidence',
    'delivery-evidence',
    false,
    10485760,
    array['image/jpeg', 'image/png', 'image/webp']
  )
on conflict (id) do update
set
  name = excluded.name,
  public = excluded.public,
  file_size_limit = excluded.file_size_limit,
  allowed_mime_types = excluded.allowed_mime_types;

create policy product_images_public_read
on storage.objects for select
to anon, authenticated
using (bucket_id = 'product-images');

create policy product_images_admin_insert
on storage.objects for insert
to authenticated
with check (
  bucket_id = 'product-images'
  and case
    when (storage.foldername(name))[1] ~
      '^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'
    then (select private.has_store_role(
      ((storage.foldername(name))[1])::uuid,
      array['owner', 'admin']
    ))
    else false
  end
);

create policy product_images_admin_update
on storage.objects for update
to authenticated
using (
  bucket_id = 'product-images'
  and case
    when (storage.foldername(name))[1] ~
      '^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'
    then (select private.has_store_role(
      ((storage.foldername(name))[1])::uuid,
      array['owner', 'admin']
    ))
    else false
  end
)
with check (
  bucket_id = 'product-images'
  and case
    when (storage.foldername(name))[1] ~
      '^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'
    then (select private.has_store_role(
      ((storage.foldername(name))[1])::uuid,
      array['owner', 'admin']
    ))
    else false
  end
);

create policy product_images_admin_delete
on storage.objects for delete
to authenticated
using (
  bucket_id = 'product-images'
  and case
    when (storage.foldername(name))[1] ~
      '^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'
    then (select private.has_store_role(
      ((storage.foldername(name))[1])::uuid,
      array['owner', 'admin']
    ))
    else false
  end
);

-- payment-proofs y delivery-evidence son privados. Se accede mediante URLs
-- firmadas creadas por Edge Functions; no se conceden permisos directos al
-- cliente sobre storage.objects.

commit;
