通过简单的添加一个属性，让函数自动注册到字典中。
需要配合ECAMap生成器使用，自动生成索引代码。
目前还没有实现存Delegate的索引，存的还是MethodInfo，因此有一定额外性能开销。
待有相关需求后，再改为生成Delegate索引表提高性能。