#ifndef RENODE_IMPORTS_H_
#define RENODE_IMPORTS_H_

#ifdef __cplusplus
#define EXTERN_C extern "C"
#else
#define EXTERN_C
#endif

#include "renode_imports_generated.h"

/* If this header is used as a part of tlib, we will call this hook after every callback */
#ifdef TARGET_LONG_BITS
EXTERN_C void tlib_try_interrupt_translation_block(void);
#else
#define tlib_try_interrupt_translation_block()
#endif

#define renode_direct_glue(a, b) a##b

#define renode_glue(a, b) renode_direct_glue(a, b)

#define renode_func_header(TYPE, IMPORTED_NAME, LOCAL_NAME) \
EXTERN_C void renode_glue(renode_glue(renode_glue(renode_external_attach__, renode_glue(RENODE_EXT_TYPE_, TYPE)), __), IMPORTED_NAME) (TYPE param)

#define RETURN_TYPE(TYPE) renode_glue(TYPE, _return$)

#define HAS_RETVAL_ 0
#define HAS_RETVAL_return 1
#define HAS_RETVAL2(KEYWORD) renode_direct_glue(HAS_RETVAL_, KEYWORD)
#define HAS_RETVAL(TYPE) HAS_RETVAL2(renode_glue(TYPE, _keyword$))

#define IF_THEN_ELSE(COND, THEN, ELSE) renode_direct_glue(IF_THEN_ELSE_, COND)(THEN, ELSE)
#define IF_THEN_ELSE_0(THEN, ELSE) ELSE
#define IF_THEN_ELSE_1(THEN, ELSE) THEN

#define EXTERNAL(TYPE, NAME) EXTERNAL_AS(TYPE, $##NAME, NAME)

#define EXTERNAL_AS(TYPE, IMPORTED_NAME, LOCAL_NAME) \
    TYPE renode_glue(LOCAL_NAME, _callback$);\
    renode_glue(TYPE, _return$) LOCAL_NAME (renode_glue(TYPE, _args$)) \
    {\
       /* If this function returns a value, generate code of the form \
        * uint32_t retval = (*callback)(); \
        * Otherwise, just call it. */ \
       IF_THEN_ELSE(HAS_RETVAL(TYPE), RETURN_TYPE(TYPE) retval = , ) \
       (* renode_glue(LOCAL_NAME, _callback$)) (renode_glue(TYPE, _vars$)); \
       tlib_try_interrupt_translation_block(); \
       /* If this function returns a value, return it. */ \
       IF_THEN_ELSE(HAS_RETVAL(TYPE), return retval;, ) \
    }\
    renode_func_header(TYPE, IMPORTED_NAME, LOCAL_NAME)\
    {\
      renode_glue(LOCAL_NAME, _callback$) = param;\
    }


#endif
