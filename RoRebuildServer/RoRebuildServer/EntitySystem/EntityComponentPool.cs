using System.Reflection;

namespace RoRebuildServer.EntitySystem;

public class EntityComponentPool
{
    private const int DefaultRetained = 128;

#if DEBUG
    private readonly List<FieldInfo> nullableFields = new(8);
#endif

    private readonly object[] pool;
    private int count;
    private readonly Type type;

    public EntityComponentPool(Type type)
    {
        this.type = type;
        pool = new object[128];

#if DEBUG
        var fields = type.GetFields();
        for (var i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            if (!Attribute.IsDefined(field, typeof(EntityIgnoreNullCheckAttribute)))
            {
                var fType = field.FieldType;
                var underlying = Nullable.GetUnderlyingType(fType);
                if (!fType.IsValueType || (underlying != null && !underlying.IsValueType))
                    nullableFields.Add(field);

                if (fType == typeof(Entity))
                    nullableFields.Add(field);
            }
        }
#endif
    }

    public object Get()
    {
        if (count == 0)
            return Activator.CreateInstance(type)!;

        count--;
        return pool[count];
    }

    public T Get<T>()
    {
        if (count == 0)
            return (T)Activator.CreateInstance(type)!;

        count--;
        return (T)pool[count];
    }

    public void Return(object e)
    {
        if (e is IEntityAutoReset reset)
            reset.Reset();

#if DEBUG
        if (type != e.GetType())
            throw new Exception("Attempting to return incorrect type to EntityComponentPool!");

        for (var i = 0; i < nullableFields.Count; i++)
        {
            if (nullableFields[i].FieldType.IsValueType)
            {
                if (nullableFields[i].FieldType == typeof(Entity) && ((Entity)nullableFields[i].GetValue(e)!).IsAlive())
                    throw new Exception(
                        $"Memory leak when returning entity component {type.Name}, field {nullableFields[i].Name} is not assigned Entity.Null!");
            }
            else
            {
                if (nullableFields[i].GetValue(e) != null)
                    throw new Exception(
                        $"Memory leak when returning entity component {type.Name}, field {nullableFields[i].Name} is not null!");
            }
        }
#endif

        if (count >= DefaultRetained)
            return;

        pool[count++] = e;
    }
}