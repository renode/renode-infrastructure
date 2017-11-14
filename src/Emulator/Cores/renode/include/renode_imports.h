#ifndef RENODE_IMPORTS_H_
#define RENODE_IMPORTS_H_

#include "renode_imports_generated.h"

#define renode_direct_glue(a, b) a##b

#define renode_glue(a, b) renode_direct_glue(a, b)

#define renode_func_header(TYPE, IMPORTED_NAME, LOCAL_NAME) \
void renode_glue(renode_glue(renode_glue(renode_external_attach__, renode_glue(RENODE_EXT_TYPE_, TYPE)), __), IMPORTED_NAME) (TYPE param)

#define EXTERNAL(TYPE, NAME) EXTERNAL_AS(TYPE, $##NAME, NAME)

#define EXTERNAL_AS(TYPE, IMPORTED_NAME, LOCAL_NAME) \
    TYPE renode_glue(LOCAL_NAME, _callback$);\
    renode_glue(TYPE, _return$) LOCAL_NAME (renode_glue(TYPE, _args$)) \
    {\
       renode_glue(TYPE, _keyword$) (* renode_glue(LOCAL_NAME, _callback$)) (renode_glue(TYPE, _vars$)); \
    }\
    renode_func_header(TYPE, IMPORTED_NAME, LOCAL_NAME)\
    {\
      renode_glue(LOCAL_NAME, _callback$) = param;\
    }


#endif
