Low overhead collection types with:

- **Immutability**: Once constructed, the collections cannot be changed anymore. Efficient construction can be done using so called Builders.
- **Value equality**: Two collections are considered "equal" when they have the same type and the same content.

The combination of these two properties neatly complement C# `record` types and streamline the implementation of Value Objects (DDD).

In general, the performance and memory usage is equivalent to the regular `System.Collections.Generic` types. Converting a Builder to an immutable instance is an `O(1)` operation.

---

**Documentation & more information at: https://badeend.github.io/ValueCollections/**

---

### Shameless self-promotion

May I interest you in one of my other packages?

- **[Badeend.ValueCollections](https://badeend.github.io/ValueCollections/)**: _Low overhead immutable collection types with structural equality._
- **[Badeend.Result](https://badeend.github.io/Result/)**: _For failures that are not exceptional: `Result<T,E>` for C#._
- **[Badeend.Any](https://badeend.github.io/Any/)**: _Holds any value of any type, without boxing small structs (up to 8 bytes)._
- **[Badeend.Nothing](https://github.com/badeend/Nothing)**: _If you want to use `void` as a type parameter, but C# won't let you._