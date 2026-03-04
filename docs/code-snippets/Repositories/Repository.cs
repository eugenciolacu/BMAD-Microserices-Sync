using Microsoft.EntityFrameworkCore;
using ServerService.Data;
using ServerService.Models.Grid;
using System.Linq.Expressions;

namespace ServerService.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task<IEnumerable<T>> GetPagedAsync(int pageNumber, int pageSize, string? sortBy = null, string? sortOrder = null, JqGridFilter? filter = null)
        {
            IQueryable<T> query = _dbSet;

            // Apply filters
            if (filter != null && filter.Rules.Any())
            {
                query = ApplyFilters(query, filter);
            }

            // Apply sorting if specified
            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                var parameter = Expression.Parameter(typeof(T), "x");
                var property = Expression.Property(parameter, sortBy);
                var lambda = Expression.Lambda(property, parameter);

                var methodName = sortOrder?.ToLower() == "desc" ? "OrderByDescending" : "OrderBy";
                var resultExpression = Expression.Call(
                    typeof(Queryable),
                    methodName,
                    new Type[] { typeof(T), property.Type },
                    query.Expression,
                    Expression.Quote(lambda));

                query = query.Provider.CreateQuery<T>(resultExpression);
            }

            return await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public virtual async Task<int> CountAsync()
        {
            return await _dbSet.CountAsync();
        }

        public virtual async Task<int> CountAsync(JqGridFilter? filter)
        {
            if (filter == null || !filter.Rules.Any())
                return await CountAsync();

            IQueryable<T> query = _dbSet;
            query = ApplyFilters(query, filter);
            return await query.CountAsync();
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public virtual async Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            await _context.SaveChangesAsync();
        }

        public virtual async Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public virtual async Task DeleteByIdAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                await DeleteAsync(entity);
            }
        }

        public virtual async Task<bool> ExistsAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            return entity != null;
        }

        private IQueryable<T> ApplyFilters(IQueryable<T> query, JqGridFilter filter)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            Expression? combinedExpression = null;

            foreach (var rule in filter.Rules)
            {
                // Normalize field name (capitalize first letter)
                var fieldName = char.ToUpper(rule.Field[0]) + rule.Field.Substring(1);

                var property = Expression.Property(parameter, fieldName);
                var propertyType = property.Type;

                Expression filterExpression;

                // Handle different property types
                if (propertyType == typeof(string))
                {
                    var constant = Expression.Constant(rule.Data);
                    filterExpression = rule.Op.ToLower() switch
                    {
                        "eq" => Expression.Equal(property, constant),
                        "ne" => Expression.NotEqual(property, constant),
                        "cn" => Expression.Call(property, typeof(string).GetMethod("Contains", new[] { typeof(string) })!, constant),
                        "bw" => Expression.Call(property, typeof(string).GetMethod("StartsWith", new[] { typeof(string) })!, constant),
                        "ew" => Expression.Call(property, typeof(string).GetMethod("EndsWith", new[] { typeof(string) })!, constant),
                        _ => Expression.Constant(true)
                    };
                }
                else if (propertyType == typeof(int) || propertyType == typeof(int?))
                {
                    if (int.TryParse(rule.Data, out var intValue))
                    {
                        var constant = Expression.Constant(intValue, typeof(int));
                        var convertedProperty = propertyType == typeof(int?)
                            ? Expression.Property(property, "Value")
                            : property;

                        filterExpression = rule.Op.ToLower() switch
                        {
                            "eq" => Expression.Equal(convertedProperty, constant),
                            "ne" => Expression.NotEqual(convertedProperty, constant),
                            "lt" => Expression.LessThan(convertedProperty, constant),
                            "le" => Expression.LessThanOrEqual(convertedProperty, constant),
                            "gt" => Expression.GreaterThan(convertedProperty, constant),
                            "ge" => Expression.GreaterThanOrEqual(convertedProperty, constant),
                            _ => Expression.Constant(true)
                        };
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }

                combinedExpression = combinedExpression == null
                    ? filterExpression
                    : filter.GroupOp.ToUpper() == "OR"
                        ? Expression.OrElse(combinedExpression, filterExpression)
                        : Expression.AndAlso(combinedExpression, filterExpression);
            }

            if (combinedExpression != null)
            {
                var lambda = Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);
                query = query.Where(lambda);
            }

            return query;
        }
    }
}