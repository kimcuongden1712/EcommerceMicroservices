'SHOW CREATE TABLE hữu ích nhất để xem DDL thực tế, bao gồm primary/foreign key, index, collation và check constraint. Đối chiếu kết quả với migration và tài liệu.'
SHOW TABLES;
SHOW CREATE TABLE categories;
SHOW CREATE TABLE products;
SHOW CREATE TABLE product_variants;
SHOW CREATE TABLE product_images;
SHOW CREATE TABLE product_image_uploads;
SHOW CREATE TABLE catalog_audit;
SHOW CREATE TABLE catalog_outbox;

'rà index trên tất cả bảng'
SELECT
    table_name, index_name, non_unique, seq_in_index, column_name
FROM information_schema.statistics
WHERE table_schema = 'ProductDb'
ORDER BY table_name, index_name, seq_in_index;


'xem constraint:'
SELECT
    table_name, constraint_name, constraint_type
FROM information_schema.table_constraints
WHERE constraint_schema = 'ProductDb'
ORDER BY table_name, constraint_type, constraint_name;

'MySQL lưu check constraint riêng; có thể kiểm tra bằng:'
SELECT
    tc.table_name, tc.constraint_name, cc.check_clause
FROM information_schema.table_constraints AS tc
JOIN information_schema.check_constraints AS cc
  ON cc.constraint_schema = tc.constraint_schema
 AND cc.constraint_name = tc.constraint_name
WHERE tc.constraint_schema = 'ProductDb'
  AND tc.constraint_type = 'CHECK';

'kiểm tra charset/collation của cột'
SELECT
    table_name, column_name, column_type, is_nullable, collation_name
FROM information_schema.columns
WHERE table_schema = 'ProductDb'
ORDER BY table_name, ordinal_position;